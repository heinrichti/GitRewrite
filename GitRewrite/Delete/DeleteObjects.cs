using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
            var rewrittenCommits = RemoveObjectsFromTree(repositoryPath, fileDeleteStrategies, folderDeleteStrategies);
            if (rewrittenCommits.Any())
                Refs.Update(repositoryPath, rewrittenCommits);
        }

        public static Dictionary<ObjectHash, ObjectHash> RemoveObjectsFromTree(string vcsPath,
            FileDeletionStrategies filesToDelete, FolderDeletionStrategies foldersToDelete)
        {
            var rewrittenCommits = new Dictionary<ObjectHash, ObjectHash>();
            var rewrittenTrees = new ConcurrentDictionary<ObjectHash, ObjectHash>();

            foreach (var commit in CommitWalker
                .CommitsInOrder(vcsPath))
            {
                var newTreeHash = RemoveObjectFromTree(vcsPath, commit.TreeHash, filesToDelete, foldersToDelete, rewrittenTrees, new byte[0]);
                if (newTreeHash != commit.TreeHash)
                {
                    var newCommit = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, newTreeHash,
                        Hash.GetRewrittenParentHashes(commit.Parents, rewrittenCommits));

                    var newCommitBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommit);
                    var newCommitHash = new ObjectHash(Hash.Create(newCommitBytes));

                    if (newCommitHash != commit.Hash)
                    {
                        HashContent.WriteFile(vcsPath, newCommitBytes, newCommitHash.ToString());
                        rewrittenCommits.TryAdd(commit.Hash, newCommitHash);
                    }
                }
            }

            return rewrittenCommits;
        }

        private static readonly ArrayPool<byte> FilePathPool = ArrayPool<byte>.Shared;
        private static readonly TreeLineByHashComparer TreeLineByHashComparer = new TreeLineByHashComparer();

        private static ObjectHash RemoveObjectFromTree(
            string vcsPath,
            ObjectHash treeHash,
            FileDeletionStrategies filesToRemove,
            FolderDeletionStrategies foldersToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees,
            ReadOnlySpan<byte> currentPath)
        {
            if (rewrittenTrees.TryGetValue(treeHash, out var rewrittenHash))
                return rewrittenHash;

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
                                path);

                            FilePathPool.Return(rentedPathBytes);

                            resultingLines.Add(new Tree.TreeLine(line.TextBytes, newTreeHash));
                        }
                        else
                        {
                            
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
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }
    }
}