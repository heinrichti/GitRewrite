using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitRewrite.GitObjects;

namespace GitRewrite
{
    public static class CommitWalker
    {
        public static IEnumerable<Commit> CommitsRandomOrder(string vcsPath)
        {
            var commitsAlreadySeen = new HashSet<ObjectHash>();
            var commits = ReadCommitsFromRefs(vcsPath);
            while (commits.TryPop(out var commit))
            {
                if (!commitsAlreadySeen.Add(commit.Hash))
                    continue;

                yield return commit;

                foreach (var parent in commit.Parents)
                    commits.Push(GitObjectFactory.ReadCommit(vcsPath, parent));
            }
        }

        public static IEnumerable<Commit> CommitsInOrder(string vcsPath)
        {
            var commits = ReadCommitsFromRefs(vcsPath);
            var parentsSeen = new HashSet<ObjectHash>();
            var commitsProcessed = new HashSet<ObjectHash>();

            while (commits.TryPop(out var commit))
                if (commitsProcessed.Contains(commit.Hash))
                    parentsSeen.Remove(commit.Hash);
                else
                {
                    if (!parentsSeen.Add(commit.Hash) || !commit.HasParents)
                    {
                        commitsProcessed.Add(commit.Hash);
                        yield return commit;
                    }
                    else
                    {
                        commits.Push(commit);
                        foreach (var parent in commit.Parents)
                        {
                            if (!commitsProcessed.Contains(parent))
                            {
                                var parentCommit = GitObjectFactory.ReadCommit(vcsPath, parent);
                                if (parentCommit == null)
                                    throw new Exception("Commit not found: " + parent);
                                commits.Push(parentCommit);
                            }
                        }
                    }
                }
        }

        private static ConcurrentStack<Commit> ReadCommitsFromRefs(string vcsPath)
        {
            var refs = Refs.ReadAll(vcsPath);

            var result = new ConcurrentStack<Commit>();

            Parallel.ForEach(refs, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, @ref =>
             {
                 var gitObject = @ref is TagRef tag
                     ? GitObjectFactory.ReadGitObject(vcsPath, new ObjectHash(tag.CommitHash))
                     : GitObjectFactory.ReadGitObject(vcsPath, new ObjectHash(@ref.Hash));

                 while (gitObject is Tag tagObject)
                 {
                     gitObject = GitObjectFactory.ReadGitObject(vcsPath, new ObjectHash(tagObject.Object));
                 }

                 if (gitObject.Type == GitObjectType.Commit)
                     result.Push((Commit)gitObject);
                 // Tags pointing to trees are ignored
                 else if (gitObject.Type != GitObjectType.Tree)
                     throw new Exception();
             });

            return result;
        }
    }
}