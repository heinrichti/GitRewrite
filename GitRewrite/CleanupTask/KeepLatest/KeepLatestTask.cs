using System;
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
        private HashSet<ObjectHash> _commitsToSkip;

        public KeepLatestTask(string repositoryPath, string fileToDelete, int protectedRevisionsCount)
            : base(repositoryPath)
        {
            _commitsToSkip = new HashSet<ObjectHash>();

            _fileName = System.Text.Encoding.UTF8.GetBytes(fileToDelete);

            var commits = CommitWalker.ReadCommitsFromRefs(RepositoryPath);
            var commitsProcessed = new HashSet<ObjectHash>();
            var treesVisited = new Dictionary<ObjectHash, bool>();

            var commitsToProtect = new SortedList<long, Commit>();

            while (commits.TryPop(out var commit))
            {
                if (!commitsProcessed.Add(commit.Hash))
                    continue;

                foreach (var parent in commit.Parents.Where(p => !commitsProcessed.Contains(p)))
                {
                    commits.Push(GitObjectFactory.ReadCommit(RepositoryPath, parent));
                }

                if (TreeContainsObject(commit.TreeHash, treesVisited))
                {
                    // file is in the commit, candidate for protection
                    if (commitsToProtect.Count < protectedRevisionsCount)
                        commitsToProtect.Add(long.Parse(commit.GetCommitTime()), commit);
                    else
                    {
                        var time = long.Parse(commit.GetCommitTime());
                        long removeTime = -1;

                        foreach (var item in commitsToProtect)
                        {
                            if (item.Key < time)
                            {
                                removeTime = item.Key;
                                break;
                            }
                        }

                        if (removeTime != -1)
                        {
                            commitsToProtect.Remove(removeTime);
                            commitsToProtect.Add(time, commit);
                        }

                        break;
                    }
                }
            }

            _commitsToSkip = new HashSet<ObjectHash>(commitsToProtect.Select(x => x.Value.Hash));
        }

        protected override (Commit Commit, ObjectHash NewTreeHash) ParallelStep(Commit commit)
            => (commit, _commitsToSkip.Contains(commit.Hash)
                ? commit.TreeHash
                : RemoveObjectFromTree(RepositoryPath, commit.TreeHash,
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

        public bool DeleteObject(in ReadOnlySpan<byte> fileName) =>
            fileName.SpanEquals(_fileName);

        private bool TreeContainsObject(
            ObjectHash treeHash,
            Dictionary<ObjectHash, bool> treesVisited)
        {
            if (treesVisited.TryGetValue(treeHash, out var result))
                return result;

            var tree = GitObjectFactory.ReadTree(RepositoryPath, treeHash);
            foreach (var line in tree.Lines)
            {
                if (line.IsDirectory())
                {
                    if (TreeContainsObject(line.Hash, treesVisited))
                    {
                        treesVisited.Add(treeHash, true);
                        return true;
                    }
                }
                else if (DeleteObject(line.FileNameBytes.Span))
                {
                    treesVisited.Add(treeHash, true);
                    return true;
                }
            }

            treesVisited.Add(treeHash, false);
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

            //if (!IsPathRelevant(currentPath, relevantPathes))
            //    return treeHash;

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
                    if (!DeleteObject(line.FileNameBytes.Span))
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

        private static bool IsPathRelevant(in ReadOnlySpan<byte> currentPath, List<byte[]> relevantPathes)
        {
            if (currentPath.Length == 0 || !relevantPathes.Any())
                return true;

            for (var i = relevantPathes.Count - 1; i >= 0; i--)
            {
                var path = relevantPathes[i];

                if (currentPath.Length > path.Length)
                    continue;

                var isRelevant = true;
                for (var j = currentPath.Length - 1; j >= 0; j--)
                    if (currentPath[j] != path[j])
                    {
                        isRelevant = false;
                        break;
                    }

                if (isRelevant)
                    return true;
            }

            return false;
        }
    }
}