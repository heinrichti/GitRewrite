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

                foreach (var parent in commit.Parents)
                    commits.Push(GitObjectFactory.ReadCommit(vcsPath, parent));

                yield return commit;
            }
        }

        public static IEnumerable<Commit> CommitsInOrder(string vcsPath)
        {
            var commits = ReadCommitsFromRefs(vcsPath);
            var parentsSeen = new HashSet<ObjectHash>();

            while (commits.TryPop(out var commit))
                if (!parentsSeen.Add(commit.Hash) || !commit.HasParents)
                    yield return commit;
                else
                {
                    commits.Push(commit);
                    foreach (var parent in commit.Parents)
                    {
                        var parentCommit = GitObjectFactory.ReadCommit(vcsPath, parent);
                        if (parentCommit == null)
                            throw new Exception("Commit not found: " + parent);
                        commits.Push(parentCommit);
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
                 else
                     throw new Exception();
             });

            return result;
        }
    }
}