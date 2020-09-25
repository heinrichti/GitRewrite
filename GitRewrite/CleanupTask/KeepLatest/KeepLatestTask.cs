﻿using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask.KeepLatest
{
    public class KeepLatestTask : CleanupTaskBase<(Commit Commit, ObjectHash NewTreeHash)>
    {
        private static readonly TreeLineByHashComparer TreeLineByHashComparer = new TreeLineByHashComparer();
        private static readonly ArrayPool<byte> FilePathPool = ArrayPool<byte>.Shared;
        private readonly byte[] _fileName;

        private readonly ConcurrentDictionary<ObjectHash, ObjectHash> _rewrittenTrees =
            new ConcurrentDictionary<ObjectHash, ObjectHash>();
        private HashSet<ObjectHash> _blobsToProtect;

        public KeepLatestTask(string repositoryPath, string fileToDelete, int protectedRevisionsCount)
            : base(repositoryPath)
        {
            _blobsToProtect = new HashSet<ObjectHash>();

            _fileName = System.Text.Encoding.UTF8.GetBytes(fileToDelete);

            var commits = CommitWalker.ReadCommitsFromRefs(RepositoryPath);
            var commitsProcessed = new HashSet<ObjectHash>();
            var treesVisited = new Dictionary<ObjectHash, ObjectHash>();

            var blobsToProtect = new Dictionary<ObjectHash, long>();

            while (commits.TryPop(out var commit))
            {
                if (!commitsProcessed.Add(commit.Hash))
                    continue;

                foreach (var parent in commit.Parents.Where(p => !commitsProcessed.Contains(p)))
                {
                    commits.Push(GitObjectFactory.ReadCommit(RepositoryPath, parent));
                }

                if (TreeContainsObject(commit.TreeHash, treesVisited, out var blobHash))
                {
                    if (blobsToProtect.TryGetValue(blobHash, out var changedTime))
                    {
                        // the same blob was already seen, update commit time if this is earlier
                        if (changedTime > commit.GetCommitTime())
                            blobsToProtect[blobHash] = commit.GetCommitTime();

                        continue;
                    }

                    // file is in the commit, candidate for protection
                    if (blobsToProtect.Count < protectedRevisionsCount)
                        blobsToProtect.Add(blobHash, commit.GetCommitTime());
                    else
                    {
                        var commitTime = commit.GetCommitTime();
                        long blobToReplaceCommitTime = -1;
                        ObjectHash blobToReplace = ObjectHash.Empty;

                        foreach (var item in blobsToProtect)
                        {
                            if (item.Value < commitTime && blobToReplaceCommitTime < commitTime)
                            {
                                blobToReplaceCommitTime = commitTime;
                                blobToReplace = item.Key;
                            }
                        }

                        if (blobToReplace != ObjectHash.Empty)
                        {
                            blobsToProtect.Remove(blobToReplace);
                            blobsToProtect.Add(blobToReplace, commitTime);
                        }
                    }
                }
            }

            _blobsToProtect = new HashSet<ObjectHash>(blobsToProtect.Select(x => x.Key));
        }

        protected override (Commit Commit, ObjectHash NewTreeHash) ParallelStep(Commit commit)
            => (commit, RemoveObjectFromTree(RepositoryPath, commit.TreeHash,
                    _rewrittenTrees, new byte[0]));

        protected override void SynchronousStep((Commit Commit, ObjectHash NewTreeHash) removalResult)
        {
            var rewrittenParentHashes = GetRewrittenCommitHashes(removalResult.Commit.Parents).ToList();

            if (removalResult.NewTreeHash != removalResult.Commit.TreeHash || !rewrittenParentHashes.SequenceEqual(removalResult.Commit.Parents))
            {
                var newCommit = Commit.GetSerializedCommitWithChangedTreeAndParents(removalResult.Commit,
                    removalResult.NewTreeHash,
                    rewrittenParentHashes);

                var newCommitBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommit);
                var newCommitHash = new ObjectHash(Hash.Create(newCommitBytes));

                EnqueueCommitWrite(removalResult.Commit.Hash, newCommitHash, newCommitBytes);
            }
        }

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, in ObjectHash blobHash) =>
            fileName.SpanEquals(_fileName) && !_blobsToProtect.Contains(blobHash);

        private bool TreeContainsObject(
            ObjectHash treeHash,
            Dictionary<ObjectHash, ObjectHash> treesVisited,
            out ObjectHash foundTreeHash)
        {
            if (treesVisited.TryGetValue(treeHash, out foundTreeHash))
            {
                return foundTreeHash != ObjectHash.Empty;
            }

            var tree = GitObjectFactory.ReadTree(RepositoryPath, treeHash);
            foreach (var line in tree.Lines)
            {
                if (line.IsDirectory())
                {
                    if (TreeContainsObject(line.Hash, treesVisited, out foundTreeHash))
                    {
                        treesVisited.Add(treeHash, foundTreeHash);
                        return foundTreeHash != ObjectHash.Empty;
                    }
                }
                else if (DeleteObject(line.FileNameBytes.Span, line.Hash))
                {
                    treesVisited.Add(treeHash, line.Hash);
                    foundTreeHash = line.Hash;
                    return true;
                }
            }

            treesVisited.Add(treeHash, ObjectHash.Empty);
            return false;
        }

        private ObjectHash RemoveObjectFromTree(
            string vcsPath,
            ObjectHash treeHash,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees,
            in ReadOnlySpan<byte> currentPath)
        {
            if (rewrittenTrees.TryGetValue(treeHash, out var rewrittenHash))
                return rewrittenHash;

            var tree = GitObjectFactory.ReadTree(vcsPath, treeHash);
            var resultingLines = new List<Tree.TreeLine>();
            foreach (var line in tree.Lines)
                if (line.IsDirectory())
                {
                    if (rewrittenTrees.TryGetValue(line.Hash, out var newHash))
                    {
                        resultingLines.Add(new Tree.TreeLine(line.TextBytes, newHash));
                    }
                    else
                    {
                        var pathLength = currentPath.Length + line.FileNameBytes.Length + 1;
                        var rentedPathBytes = FilePathPool.Rent(pathLength);
                        var path = rentedPathBytes.AsSpan(0, pathLength);
                        currentPath.CopyTo(path);
                        path[currentPath.Length] = (byte) '/';
                        line.FileNameBytes.Span.CopyTo(path.Slice(currentPath.Length + 1));
                    }
                }
                else
                {
                    if (!DeleteObject(line.FileNameBytes.Span, line.Hash))
                        resultingLines.Add(line);
                }

            if (resultingLines.Count == tree.Lines.Count &&
                resultingLines.SequenceEqual(tree.Lines, TreeLineByHashComparer))
            {
                rewrittenTrees.TryAdd(tree.Hash, tree.Hash);
                return tree.Hash;
            }

            var fixedTree = Tree.GetFixedTree(resultingLines);
            if (fixedTree.Hash != tree.Hash)
            {
                var bytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Tree, fixedTree.SerializeToBytes());
                EnqueueTreeWrite(fixedTree.Hash, bytes);
            }

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }
    }
}