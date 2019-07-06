using System.Collections.Generic;
using System.Linq;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask
{
    class RemoveEmptyCommitsTask : CleanupTaskBase<Commit>
    {
        public RemoveEmptyCommitsTask(string repositoryPath) : base(repositoryPath)
        {
        }

        protected override Commit ParallelStep(Commit commit) => commit;

        private readonly Dictionary<ObjectHash, ObjectHash> _commitsWithTreeHashes = new Dictionary<ObjectHash, ObjectHash>();

        protected override void SynchronousStep(Commit commit)
        {
            if (commit.Parents.Count == 1)
            {
                var parentHash = GetRewrittenCommitHash(commit.Parents.Single());
                var parentTreeHash = _commitsWithTreeHashes[parentHash];
                if (parentTreeHash == commit.TreeHash)
                {
                    // This commit will be removed
                    RegisterCommitChange(commit.Hash, parentHash);
                    return;
                }
            }

            // rewrite this commit
            var correctParents = GetRewrittenCommitHashes(commit.Parents).ToList();
            byte[] newCommitBytes;
            if (correctParents.SequenceEqual(commit.Parents))
                newCommitBytes = commit.SerializeToBytes();
            else
                newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit, commit.TreeHash,
                    correctParents);

            var resultBytes = GitObjectFactory.GetBytesWithHeader(GitObjectType.Commit, newCommitBytes);

            var newCommitHash = new ObjectHash(Hash.Create(resultBytes));
            var newCommit = new Commit(newCommitHash, newCommitBytes);

            _commitsWithTreeHashes.TryAdd(newCommitHash, newCommit.TreeHash);

            EnqueueCommitWrite(commit.Hash, newCommitHash, resultBytes);
        }
    }
}
