using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitRewrite.GitObjects;
using GitRewrite.IO;

namespace GitRewrite.Delete
{
    internal class DeleteFiles
    {
        public static void Run(string repositoryPath, IEnumerable<string> filesToDelete)
        {
            var fileDeleteStrategies = new FileDeleteStrategies(filesToDelete);
            var rewrittenCommits = RemoveFiles(repositoryPath, fileDeleteStrategies);
            if (rewrittenCommits.Any())
                Refs.Update(repositoryPath, rewrittenCommits);
        }

        public static Dictionary<ObjectHash, ObjectHash> RemoveFiles(
            string vcsPath,
            FileDeleteStrategies filesToRemove)
        {
            var rewrittenCommits = new Dictionary<ObjectHash, ObjectHash>();
            var rewrittenTrees = new ConcurrentDictionary<ObjectHash, ObjectHash>();

            foreach (var commit in CommitWalker
                .CommitsInOrder(vcsPath))
            {
                var newTreeHash = RemoveFileFromRootTree(vcsPath, commit.TreeHash, filesToRemove, rewrittenTrees);
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

            return rewrittenCommits;
        }

        private static ObjectHash RemoveFileFromRootTree(
            string vcsPath, ObjectHash treeHash,
            FileDeleteStrategies filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
        {
            if (rewrittenTrees.TryGetValue(treeHash, out var rewrittenHash))
                return rewrittenHash;

            var tree = GitObjectFactory.ReadTree(vcsPath, treeHash);
            var resultingLines = new List<Tree.TreeLine>();

            foreach (var line in tree.Lines)
            {
                var rewrittenLine = RemoveFileFromLine(
                    vcsPath,
                    line,
                    filesToRemove,
                    rewrittenTrees);
                if (rewrittenLine != null)
                    resultingLines.Add(line);
            }

            var fixedTree = Tree.GetFixedTree(resultingLines);
            if (fixedTree.Hash != tree.Hash)
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }

        private static readonly ArrayPool<byte> FilePathPool = ArrayPool<byte>.Shared;

        private static Tree.TreeLine RemoveFileFromLine(string vcsPath,
            Tree.TreeLine line,
            FileDeleteStrategies filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
        {
            if (line.IsDirectory())
            {
                if (rewrittenTrees.TryGetValue(line.Hash, out var newHash))
                    return new Tree.TreeLine(line.TextBytes, newHash);

                var pathLength = line.FileNameBytes.Length + 1;
                var pathBytes = FilePathPool.Rent(pathLength);
                var path = pathBytes.AsSpan(0, pathLength);
                path[0] = (byte)'/';
                line.FileNameBytes.Span.CopyTo(path.Slice(1));
                var newTreeHash = RemoveFileFromTree(
                    vcsPath,
                    line.Hash,
                    filesToRemove,
                    rewrittenTrees,
                    path);
                FilePathPool.Return(pathBytes);
                return new Tree.TreeLine(line.TextBytes, newTreeHash);
            }

            if (!filesToRemove.DeleteFile(line.FileNameBytes.Span, new byte[0]))
                return line;

            return null;
        }

        private static ObjectHash RemoveFileFromTree(
            string vcsPath,
            ObjectHash treeHash,
            FileDeleteStrategies filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees,
            ReadOnlySpan<byte> currentPath)
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
                        
                        var newTreeHash = RemoveFileFromTree(
                            vcsPath,
                            line.Hash,
                            filesToRemove,
                            rewrittenTrees,
                            path);

                        FilePathPool.Return(rentedPathBytes);

                        resultingLines.Add(new Tree.TreeLine(line.TextBytes, newTreeHash));
                    }
                }
                else
                {
                    if (!filesToRemove.DeleteFile(line.FileNameBytes.Span, currentPath))
                        resultingLines.Add(line);
                }

            var fixedTree = Tree.GetFixedTree(resultingLines);
            if (fixedTree.Hash != tree.Hash)
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }
    }
}