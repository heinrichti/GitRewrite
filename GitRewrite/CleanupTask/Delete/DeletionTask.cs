using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask.Delete
{
    public class DeletionTask : CleanupTaskBase<(Commit Commit, ObjectHash NewTreeHash)>
    {
        private static readonly TreeLineByHashComparer TreeLineByHashComparer = new TreeLineByHashComparer();
        private static readonly ArrayPool<byte> FilePathPool = ArrayPool<byte>.Shared;
        private readonly FileDeletionStrategies _fileDeleteStrategies;
        private readonly FolderDeletionStrategies _folderDeleteStrategies;
        private readonly List<byte[]> _relevantPathes;

        private readonly ConcurrentDictionary<ObjectHash, ObjectHash> _rewrittenTrees =
            new ConcurrentDictionary<ObjectHash, ObjectHash>();

        public DeletionTask(string repositoryPath, IEnumerable<string> filesToDelete,
            IEnumerable<string> foldersToDelete)
            : base(repositoryPath)
        {
            _fileDeleteStrategies = new FileDeletionStrategies(filesToDelete);
            _folderDeleteStrategies = new FolderDeletionStrategies(foldersToDelete);

            _relevantPathes =
                _fileDeleteStrategies.RelevantPaths.Union(_folderDeleteStrategies.RelevantPaths).ToList();
        }

        protected override (Commit Commit, ObjectHash NewTreeHash) ParallelStep(Commit commit)
            => (commit, RemoveObjectFromTree(RepositoryPath, commit.TreeHash, _fileDeleteStrategies,
                _folderDeleteStrategies,
                _rewrittenTrees, new byte[0], _relevantPathes));

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

        private ObjectHash RemoveObjectFromTree(
            string vcsPath,
            ObjectHash treeHash,
            FileDeletionStrategies filesToRemove,
            FolderDeletionStrategies foldersToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees,
            in ReadOnlySpan<byte> currentPath,
            List<byte[]> relevantPathes)
        {
            if (rewrittenTrees.TryGetValue(treeHash, out var rewrittenHash))
                return rewrittenHash;

            if (!IsPathRelevant(currentPath, relevantPathes))
                return treeHash;

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

                        if (!foldersToRemove.DeleteObject(path))
                        {
                            var newTreeHash = RemoveObjectFromTree(
                                vcsPath,
                                line.Hash,
                                filesToRemove,
                                foldersToRemove,
                                rewrittenTrees,
                                path,
                                relevantPathes);

                            FilePathPool.Return(rentedPathBytes);

                            resultingLines.Add(new Tree.TreeLine(line.TextBytes, newTreeHash));
                        }
                    }
                }
                else
                {
                    if (!filesToRemove.DeleteObject(line.FileNameBytes.Span, currentPath))
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