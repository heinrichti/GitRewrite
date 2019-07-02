using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GitRewrite.GitObjects;
using GitRewrite.IO;

namespace GitRewrite.Delete
{
    internal class DeleteObjects
    {
        public static void Run(string repositoryPath, IEnumerable<string> filesToDelete, IEnumerable<string> foldersToDelete)
        {
            var fileDeleteStrategies = new FileDeletionStrategies(filesToDelete);
            var folderDeleteStrategies = new FolderDeletionStrategies(foldersToDelete);

            var relevantPathes =
                fileDeleteStrategies.RelevantPaths.Union(folderDeleteStrategies.RelevantPaths).ToList();

            var rewrittenCommits = RemoveObjectsFromTree(repositoryPath, fileDeleteStrategies, folderDeleteStrategies,
                relevantPathes);

            if (rewrittenCommits.Any())
                Refs.Update(repositoryPath, rewrittenCommits);
        }

        private static readonly BlockingCollection<(byte[] Bytes, ObjectHash Hash)> ObjectsToWrite =
            new BlockingCollection<(byte[] Bytes, ObjectHash Hash)>();

        public static Dictionary<ObjectHash, ObjectHash> RemoveObjectsFromTree(string vcsPath,
            FileDeletionStrategies filesToDelete, FolderDeletionStrategies foldersToDelete,
            List<byte[]> relevantPaths)
        {
            var rewrittenCommits = new Dictionary<ObjectHash, ObjectHash>();
            var rewrittenTrees = new ConcurrentDictionary<ObjectHash, ObjectHash>();

            var writeThread = new Thread(collection =>
            {
                var commits = (BlockingCollection<(byte[] Bytes, ObjectHash Hash)>)collection;
                foreach (var commit in commits.GetConsumingEnumerable())
                    HashContent.WriteFile(vcsPath, commit.Bytes, commit.Hash.ToString());
            });
            writeThread.Start(ObjectsToWrite);

            foreach (var removalResult in CommitWalker
                .CommitsInOrder(vcsPath)
                .AsParallel()
                .AsOrdered()
                .Select(commit =>
                    (NewTreeHash:
                    RemoveObjectFromTree(vcsPath, commit.TreeHash, filesToDelete, foldersToDelete,
                        rewrittenTrees, new byte[0], relevantPaths), Commit: commit))
            )
            {
                var rewrittenParentHashes = Hash.GetRewrittenParentHashes(removalResult.Commit.Parents, rewrittenCommits).ToList();

                if (removalResult.NewTreeHash != removalResult.Commit.TreeHash || !rewrittenParentHashes.SequenceEqual(removalResult.Commit.Parents))
                {
                    var newCommit = Commit.GetSerializedCommitWithChangedTreeAndParents(removalResult.Commit,
                        removalResult.NewTreeHash,
                        rewrittenParentHashes);

                    var newCommitBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommit);
                    var newCommitHash = new ObjectHash(Hash.Create(newCommitBytes));

                    if (newCommitHash != removalResult.Commit.Hash)
                    {
                        ObjectsToWrite.Add((newCommitBytes, newCommitHash));
                        rewrittenCommits.TryAdd(removalResult.Commit.Hash, newCommitHash);
                    }
                }
            }

            ObjectsToWrite.CompleteAdding();
            writeThread.Join();
            ObjectsToWrite.Dispose();

            return rewrittenCommits;
        }

        private static readonly ArrayPool<byte> FilePathPool = ArrayPool<byte>.Shared;
        private static readonly TreeLineByHashComparer TreeLineByHashComparer = new TreeLineByHashComparer();

        private static bool IsPathRelevant(in ReadOnlySpan<byte> currentPath, List<byte[]> relevantPathes)
        {
            if (currentPath.Length == 0 || !relevantPathes.Any())
                return true;

            for (int i = relevantPathes.Count - 1; i >= 0; i--)
            {
                var path = relevantPathes[i];

                if (currentPath.Length > path.Length)
                    continue;

                var isRelevant = true;
                for (int j = currentPath.Length - 1; j >= 0; j--)
                {
                    if (currentPath[j] != path[j])
                    {
                        isRelevant = false;
                        break;
                    }
                }

                if (isRelevant) 
                    return true;
            }

            return false;
        }

        private static ObjectHash RemoveObjectFromTree(
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
            {
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
            }

            if (resultingLines.Count == tree.Lines.Count && resultingLines.SequenceEqual(tree.Lines, TreeLineByHashComparer))
            {
                rewrittenTrees.TryAdd(tree.Hash, tree.Hash);
                return tree.Hash;
            }

            var fixedTree = Tree.GetFixedTree(resultingLines);
            if (fixedTree.Hash != tree.Hash)
            {
                var bytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Tree, fixedTree.SerializeToBytes());
                ObjectsToWrite.Add((bytes, fixedTree.Hash));
            }

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }
    }
}