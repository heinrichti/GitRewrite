using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask
{
    class RewriteContributorTask : CleanupTaskBase<Commit>
    {
        private readonly Dictionary<string, string> _contributorMappings = new Dictionary<string, string>();

        public RewriteContributorTask(string repositoryPath, string contributorMappingFile) : base(repositoryPath)
        {
            var contributorMappingLines = File.ReadAllLines(contributorMappingFile);

            foreach (var line in contributorMappingLines.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var contributorMapping = line.Split('=').Select(x => x.Trim()).ToList();
                if (contributorMapping.Count != 2)
                    throw new ArgumentException("Mapping is not formatted properly.");

                _contributorMappings.Add(contributorMapping[0], contributorMapping[1]);
            }
        }

        protected override Commit ParallelStep(Commit commit) => commit;

        protected override void SynchronousStep(Commit commit)
        {
            var rewrittenParentHashes = GetRewrittenCommitHashes(commit.Parents);
            var changedCommit = commit.WithChangedContributor(_contributorMappings, rewrittenParentHashes);

            var resultBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, changedCommit);
            var newCommitHash = new ObjectHash(Hash.Create(resultBytes));

            EnqueueCommitWrite(commit.Hash, newCommitHash, resultBytes);
        }
    }
}
