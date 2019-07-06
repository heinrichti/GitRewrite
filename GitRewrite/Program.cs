using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GitRewrite.CleanupTask;
using GitRewrite.CleanupTask.Delete;
using GitRewrite.GitObjects;
using GitRewrite.IO;
using Commit = GitRewrite.GitObjects.Commit;
using Tree = GitRewrite.GitObjects.Tree;

namespace GitRewrite
{
    public static class Program
    {
        static void Main(string[] args)
        {
            if (!CommandLineOptions.TryParse(args, out var options))
            {
                return;
            }

            PackReader.InitializePackFiles(options.RepositoryPath);

            if (options.FixTrees)
            {
                var defectiveCommits = FindCommitsWithDuplicateTreeEntries(options.RepositoryPath).ToList();

                var rewrittenCommits = FixDefectiveCommits(options.RepositoryPath, defectiveCommits);
                if (rewrittenCommits.Any())
                    Refs.Update(options.RepositoryPath, rewrittenCommits);
            }
            else if (options.FilesToDelete.Any() || options.FoldersToDelete.Any())
            {
                using (var task = new DeletionTask(options.RepositoryPath, options.FilesToDelete, options.FoldersToDelete, options.ProtectRefs))
                    task.Run();
            }
            else if (options.RemoveEmptyCommits)
            {
                using (var removeEmptyCommitsTask = new RemoveEmptyCommitsTask(options.RepositoryPath))
                    removeEmptyCommitsTask.Run();
            }
            else if (!string.IsNullOrWhiteSpace(options.ContributerMappingFile))
            {
                using (var rewriteContributerTask = new RewriteContributerTask(options.RepositoryPath, options.ContributerMappingFile))
                    rewriteContributerTask.Run();
            }
            else if (options.ListContributerNames)
            {
                foreach (var contributer in CommitWalker.CommitsRandomOrder(options.RepositoryPath)
                    .SelectMany(commit => new[] {commit.GetAuthorName(), commit.GetCommitterName()})
                    .Distinct()
                    .AsParallel()
                    .OrderBy(x => x))
                    Console.WriteLine(contributer);
            }
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
