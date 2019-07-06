using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask
{
    class RewriteContributerTask : CleanupTaskBase<Commit>
    {
        private readonly Dictionary<string, string> _contributerMappings = new Dictionary<string, string>();

        public RewriteContributerTask(string repositoryPath, string contributerMappingFile) : base(repositoryPath)
        {
            var contributerMappingLines = File.ReadAllLines(contributerMappingFile);

            foreach (var line in contributerMappingLines)
            {
                var contributerMapping = line.Split('=').Select(x => x.Trim()).ToList();
                if (contributerMapping.Count != 2)
                    throw new ArgumentException("Mapping is not formatted properly.");

                _contributerMappings.Add(contributerMapping[0], contributerMapping[1]);
            }
        }

        protected override Commit ParallelStep(Commit commit) => commit;

        protected override void SynchronousStep(Commit commit)
        {
            var rewrittenParentHashes = GetRewrittenCommitHashes(commit.Parents);
            var changedCommit = commit.WithChangedContributer(_contributerMappings, rewrittenParentHashes);

            var resultBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, changedCommit);
            var newCommitHash = new ObjectHash(Hash.Create(resultBytes));

            EnqueueCommitWrite(commit.Hash, newCommitHash, resultBytes);
        }
    }
}
