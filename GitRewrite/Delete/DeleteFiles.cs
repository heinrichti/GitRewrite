using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            var resultingLines = new ConcurrentQueue<(int, Tree.TreeLine)>();

            var i = 0;

            Parallel.ForEach(tree.Lines.Select(line => (ItemIndex: i++, line)),
                new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount},
                line =>
                {
                    var rewrittenLine = RemoveFileFromLine(
                        vcsPath,
                        line.line,
                        filesToRemove,
                        rewrittenTrees);
                    if (rewrittenLine != null)
                        resultingLines.Enqueue((line.ItemIndex, rewrittenLine));
                });

            var fixedTree = Tree.GetFixedTree(resultingLines.OrderBy(x => x.Item1).Select(x => x.Item2));
            if (fixedTree.Hash != tree.Hash)
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }

        private static Tree.TreeLine RemoveFileFromLine(string vcsPath,
            Tree.TreeLine line,
            FileDeleteStrategies filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
        {
            if (line.IsDirectory())
            {
                if (rewrittenTrees.TryGetValue(line.Hash, out var newHash))
                    return new Tree.TreeLine(line.TextBytes, newHash);

                var newTreeHash = RemoveFileFromTree(
                    vcsPath,
                    line.Hash,
                    filesToRemove,
                    rewrittenTrees,
                    "/" + line.GetFileName());
                return new Tree.TreeLine(line.TextBytes, newTreeHash);
            }

            if (!filesToRemove.DeleteFile(line.FileNameBytes, ""))
                return line;

            return null;
        }

        private static ObjectHash RemoveFileFromTree(
            string vcsPath,
            ObjectHash treeHash,
            FileDeleteStrategies filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees,
            string currentPath)
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
                        var newTreeHash = RemoveFileFromTree(
                            vcsPath,
                            line.Hash,
                            filesToRemove,
                            rewrittenTrees,
                            currentPath + "/" + line.GetFileName());
                        resultingLines.Add(new Tree.TreeLine(line.TextBytes, newTreeHash));
                    }
                }
                else
                {
                    if (!filesToRemove.DeleteFile(line.FileNameBytes, currentPath))
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