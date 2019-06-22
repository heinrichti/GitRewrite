using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using CommandLine.Text;
using GitRewrite.GitObjects;
using GitRewrite.IO;
using Commit = GitRewrite.GitObjects.Commit;
using Tree = GitRewrite.GitObjects.Tree;

namespace GitRewrite
{
    public class Program
    {
        static void Main(string[] args)
        {
            var parserResult = Parser.Default.ParseArguments<CommandLineOptions>(args);
            //string helpText = HelpText.AutoBuild(parserResult);
            parserResult.WithParsed(options =>
            {
                var optionsSet = 0;
                optionsSet += options.FixTrees ? 1 : 0;
                optionsSet += options.FilesToDelete.Any() ? 1 : 0;
                optionsSet += options.RemoveEmptyCommits ? 1 : 0;

                if (optionsSet != 1)
                {
                    Console.WriteLine("Cannot mix operations. Only choose one operation at a time (multiple file deletes are allowed).");
                    Console.WriteLine("For available options see GitRewrite --help");
                    return;
                }

                if (options.FixTrees)
                {
                    var defectiveCommits = FindCommitsWithDuplicateTreeEntries(options.RepositoryPath).ToList();

                    var rewrittenCommits = FixDefectiveCommits(options.RepositoryPath, defectiveCommits);
                    if (rewrittenCommits.Any())
                        Refs.Update(options.RepositoryPath, rewrittenCommits);
                }
                else if (options.FilesToDelete.Any())
                {
                    var rewrittenCommits = RemoveFiles(options.RepositoryPath, new HashSet<string>(options.FilesToDelete));
                    if (rewrittenCommits.Any())
                        Refs.Update(options.RepositoryPath, rewrittenCommits);
                }
                else if (options.RemoveEmptyCommits)
                {
                    var rewrittenCommits = RemoveEmptyCommits(options.RepositoryPath);
                    if (rewrittenCommits.Any())
                        Refs.Update(options.RepositoryPath, rewrittenCommits);
                }
            });
        }

        public static ObjectHash WriteFixedTree(string vcsPath, Tree tree)
        {
            var resultingTreeLines = new List<Tree.TreeLine>();

            bool fixRequired = false;

            foreach (var treeLine in tree.Lines)
            {
                if (!treeLine.IsDirectory())
                {
                    resultingTreeLines.Add(treeLine);
                    continue;
                }

                var childTree = GitObjectFactory.ReadTree(vcsPath, treeLine.Hash);
                var fixedTreeHash = WriteFixedTree(vcsPath, childTree);
                resultingTreeLines.Add(new Tree.TreeLine(treeLine.TextBytes, fixedTreeHash));
                if (fixedTreeHash != childTree.Hash)
                    fixRequired = true;
            }

            if (fixRequired || Tree.HasDuplicateLines(resultingTreeLines))
            {
                tree = Tree.GetFixedTree(resultingTreeLines);
                HashContent.WriteObject(vcsPath, tree);
            }

            return tree.Hash;
        }

        private static bool HasDefectiveTree(string vcsPath, Commit commit)
        {
            if (SeenTrees.TryGetValue(commit.TreeHash, out bool isDefective))
                return isDefective;

            var tree = GitObjectFactory.ReadTree(vcsPath, commit.TreeHash);
            return IsDefectiveTree(vcsPath, tree);
        }

        private static readonly ConcurrentDictionary<ObjectHash, bool> SeenTrees = new ConcurrentDictionary<ObjectHash, bool>();

        public static bool IsDefectiveTree(string vcsPath, Tree tree)
        {
            if (SeenTrees.TryGetValue(tree.Hash, out bool isDefective))
                return isDefective;

            if (Tree.HasDuplicateLines(tree.Lines))
            {
                SeenTrees.TryAdd(tree.Hash, true);
                return true;
            }

            var childTrees = tree.GetDirectories();
            foreach (var childTree in childTrees)
            {
                if (SeenTrees.TryGetValue(childTree.Hash, out isDefective))
                {
                    if (isDefective)
                        return true;

                    continue;
                }

                var childTreeObject = (Tree) GitObjectFactory.ReadGitObject(vcsPath, childTree.Hash);
                if (IsDefectiveTree(vcsPath, childTreeObject))
                { 
                    return true;
                }
            }

            SeenTrees.TryAdd(tree.Hash, false);
            return false;
        }

        private static IEnumerable<ObjectHash> CorrectParents(IEnumerable<ObjectHash> oldParents, Dictionary<ObjectHash, ObjectHash> rewrittenCommitHashes)
        {
            foreach (var oldParentHash in oldParents)
            {
                if (rewrittenCommitHashes.TryGetValue(oldParentHash, out var newParentHash))
                    yield return newParentHash;
                else
                    yield return oldParentHash;
            }
        }

        public static Dictionary<ObjectHash, ObjectHash> RemoveFiles(string vcsPath, HashSet<string> filesToRemove)
        {
            var rewrittenCommits = new Dictionary<ObjectHash, ObjectHash>();
            var rewrittenTrees = new ConcurrentDictionary<ObjectHash, ObjectHash>();

            foreach (var commit in CommitWalker
                .CommitsInOrder(vcsPath))
            {
                var newTreeHash = RemoveFileFromRootTree(vcsPath, commit.TreeHash, filesToRemove, rewrittenTrees);
                var newCommit = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, newTreeHash,
                    GetRewrittenParentHashes(commit.Parents, rewrittenCommits));

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

        private static ObjectHash RemoveFileFromRootTree(string vcsPath, ObjectHash treeHash,
            HashSet<string> filesToRemove, ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
        {
            if (rewrittenTrees.TryGetValue(treeHash, out var rewrittenHash))
                return rewrittenHash;

            var tree = GitObjectFactory.ReadTree(vcsPath, treeHash);
            var resultingLines = new ConcurrentQueue<(int, Tree.TreeLine)>();

            int i = 0;

            Parallel.ForEach(tree.Lines.Select(line => (ItemIndex: i++, line)),
                new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount},
                line =>
                {
                    var rewrittenLine = RemovefileFromLine(vcsPath, line.line, filesToRemove, rewrittenTrees);
                    if (rewrittenLine != null)
                        resultingLines.Enqueue((line.ItemIndex, rewrittenLine));
                });

            var fixedTree = Tree.GetFixedTree(resultingLines.OrderBy(x => x.Item1).Select(x => x.Item2));
            if (fixedTree.Hash != tree.Hash)
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;

        }

        private static Tree.TreeLine RemovefileFromLine(
            string vcsPath,
            Tree.TreeLine line, 
            HashSet<string> filesToRemove,
            ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
        {
            if (line.IsDirectory())
            {
                if (rewrittenTrees.TryGetValue(line.Hash, out var newHash))
                    return new Tree.TreeLine(line.TextBytes, newHash);
                
                var newTreeHash = RemoveFileFromTree(vcsPath, line.Hash, filesToRemove, rewrittenTrees);
                return new Tree.TreeLine(line.TextBytes, newTreeHash);

            }

            if (!filesToRemove.Contains(line.Text.Substring(7)))
                return line;

            return null;
        }

        private static ObjectHash RemoveFileFromTree(string vcsPath, ObjectHash treeHash,
            HashSet<string> filesToRemove, ConcurrentDictionary<ObjectHash, ObjectHash> rewrittenTrees)
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
                        resultingLines.Add(new Tree.TreeLine(line.TextBytes, newHash));
                    else
                    {
                        var newTreeHash = RemoveFileFromTree(vcsPath, line.Hash, filesToRemove, rewrittenTrees);
                        resultingLines.Add(new Tree.TreeLine(line.TextBytes, newTreeHash));
                    }
                }
                else
                {
                    if (!filesToRemove.Contains(line.Text.Substring(7)))
                        resultingLines.Add(line);
                }
            }

            var fixedTree = Tree.GetFixedTree(resultingLines);
            if (fixedTree.Hash != tree.Hash)
                HashContent.WriteObject(vcsPath, fixedTree);

            rewrittenTrees.TryAdd(treeHash, fixedTree.Hash);

            return fixedTree.Hash;
        }

        static IEnumerable<ObjectHash> FindCommitsWithDuplicateTreeEntries(string vcsPath)
        {
            foreach (var commit in CommitWalker
                .CommitsRandomOrder(vcsPath)
                .AsParallel()
                .AsUnordered()
                .Select(commit => (commit.Hash, Defective: HasDefectiveTree(vcsPath, commit))))
            {
                if (commit.Defective)
                    yield return commit.Hash;
            }
        }

        private static Dictionary<ObjectHash, ObjectHash> RemoveEmptyCommits(string vcsPath)
        {
            var rewrittenCommitHashes = new Dictionary<ObjectHash, ObjectHash>();
            var commitsWithTreeHashes = new Dictionary<ObjectHash, ObjectHash>();

            foreach (var commit in CommitWalker.CommitsInOrder(vcsPath))
            {
                if (rewrittenCommitHashes.ContainsKey(commit.Hash))
                    continue;

                if (commit.Parents.Count() == 1)
                {
                    var parentHash = GetRewrittenParentHash(commit, rewrittenCommitHashes);
                    var parentTreeHash = commitsWithTreeHashes[parentHash];
                    if (parentTreeHash == commit.TreeHash)
                    {
                        // This commit will be removed
                        rewrittenCommitHashes.Add(commit.Hash, parentHash);
                        continue;
                    }
                }

                // rewrite this commit
                var correctParents = GetRewrittenParentHashes(commit.Parents, rewrittenCommitHashes).ToList();
                var newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, commit.TreeHash,
                    correctParents);

                var resultBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommitBytes);

                var newCommitHash = new ObjectHash(Hash.Create(resultBytes));
                var newCommit = new Commit(newCommitHash, newCommitBytes);

                commitsWithTreeHashes.TryAdd(newCommitHash, newCommit.TreeHash);

                if (newCommitHash != commit.Hash && !rewrittenCommitHashes.ContainsKey(commit.Hash))
                {
                    HashContent.WriteFile(vcsPath, resultBytes, newCommitHash.ToString());
                    rewrittenCommitHashes.Add(commit.Hash, newCommitHash);
                }
            }

            return rewrittenCommitHashes;
        }

        private static IEnumerable<ObjectHash> GetRewrittenParentHashes(IEnumerable<ObjectHash> hashes, Dictionary<ObjectHash, ObjectHash> rewrittenCommitHashes)
        {
            foreach (var parentHash in hashes)
            {
                var rewrittenParentHash = parentHash;

                while (rewrittenCommitHashes.TryGetValue(rewrittenParentHash, out var parentCommitHash))
                {
                    rewrittenParentHash = parentCommitHash;
                }

                yield return rewrittenParentHash;
            }
        }

        private static ObjectHash GetRewrittenParentHash(Commit commit, Dictionary<ObjectHash, ObjectHash> rewrittenCommitHashes)
        {
            ObjectHash parentHash = commit.Parents.Single();

            while (rewrittenCommitHashes.TryGetValue(parentHash, out var parentCommitHash))
            {
                parentHash = parentCommitHash;
            }

            return parentHash;
        }


        static Dictionary<ObjectHash, ObjectHash> FixDefectiveCommits(string vcsPath, List<ObjectHash> defectiveCommits)
        {
            var rewrittenCommitHashes = new Dictionary<ObjectHash, ObjectHash>();

            foreach (var commit in CommitWalker.CommitsInOrder(vcsPath))
            {
                if (rewrittenCommitHashes.ContainsKey(commit.Hash))
                    continue;

                // Rewrite this commit
                byte[] newCommitBytes;
                if (defectiveCommits.Contains(commit.Hash))
                {
                    var fixedTreeHash = WriteFixedTree(vcsPath, GitObjectFactory.ReadTree(vcsPath, commit.TreeHash));
                    newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, fixedTreeHash,
                        CorrectParents(commit.Parents, rewrittenCommitHashes).ToList());
                }
                else
                {
                    newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, commit.TreeHash,
                        CorrectParents(commit.Parents, rewrittenCommitHashes).ToList());
                }

                var fileObjectBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommitBytes);
                var newCommitHash = new ObjectHash(Hash.Create(fileObjectBytes));
                if (newCommitHash != commit.Hash && !rewrittenCommitHashes.ContainsKey(commit.Hash))
                {
                    HashContent.WriteFile(vcsPath, fileObjectBytes, newCommitHash.ToString());
                    rewrittenCommitHashes.Add(commit.Hash, newCommitHash);
                }
            }

            return rewrittenCommitHashes;
        }
    }
}
