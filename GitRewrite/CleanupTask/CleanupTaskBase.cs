using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GitRewrite.GitObjects;
using GitRewrite.IO;

namespace GitRewrite.CleanupTask
{
    public abstract class CleanupTaskBase<TParallelActionResult> : IDisposable
    {
        private readonly BlockingCollection<BytesToWrite> _objectsToWrite = new BlockingCollection<BytesToWrite>();
        protected readonly string RepositoryPath;

        private readonly Dictionary<ObjectHash, ObjectHash>
            _rewrittenCommits = new Dictionary<ObjectHash, ObjectHash>();

        protected CleanupTaskBase(string repositoryPath) => RepositoryPath = repositoryPath;

        public void Dispose()
        {
            _objectsToWrite.Dispose();
        }

        protected void EnqueueCommitWrite(ObjectHash oldHash, ObjectHash newHash, byte[] bytes)
        {
            if (!oldHash.Equals(newHash))
            {
                _objectsToWrite.Add(new BytesToWrite(newHash, bytes));
                RegisterCommitChange(oldHash, newHash);
            }
        }

        protected void RegisterCommitChange(ObjectHash oldHash, ObjectHash newHash)
        {
            _rewrittenCommits.Add(oldHash, newHash);
        }

        protected void EnqueueTreeWrite(ObjectHash newHash, byte[] bytes)
        {
            _objectsToWrite.Add(new BytesToWrite(newHash, bytes));
        }

        public void Run()
        {
            var writeStep = new Thread(o =>
            {
                var threadParams = (ThreadParams) o;
                var bytesToWrite = threadParams.BytesToWriteCollection;

                foreach (var commit in bytesToWrite.GetConsumingEnumerable())
                    HashContent.WriteFile(threadParams.VcsPath, commit.Bytes, commit.Hash.ToString());
            });

            writeStep.Start(new ThreadParams(RepositoryPath, _objectsToWrite));

            Console.WriteLine("Reading commits...");

            long commitNumber = 1;
            foreach (var parallelActionResult in CommitWalker.CommitsInOrder(RepositoryPath)
                .AsParallel()
                .AsOrdered()
                .Select(ParallelStep))
            {
                Console.Write("\rProcessing commit " + commitNumber++);
                SynchronousStep(parallelActionResult);
            }

            _objectsToWrite.CompleteAdding();

            Console.WriteLine();
            Console.WriteLine("Writing objects...");

            writeStep.Join();

            Console.WriteLine("Updating refs...");
            if (_rewrittenCommits.Any())
                Refs.Update(RepositoryPath, _rewrittenCommits);
        }

        protected IEnumerable<ObjectHash> GetRewrittenCommitHashes(IEnumerable<ObjectHash> hashes)
        {
            foreach (var hash in hashes)
            {
                var rewrittenHash = hash;

                while (_rewrittenCommits.TryGetValue(rewrittenHash, out var commitHash))
                    rewrittenHash = commitHash;

                yield return rewrittenHash;
            }

        }

        protected ObjectHash GetRewrittenCommitHash(ObjectHash objectHash)
        {
            while (_rewrittenCommits.TryGetValue(objectHash, out var rewrittenObjectHash))
                objectHash = rewrittenObjectHash;

            return objectHash;
        }

        protected abstract TParallelActionResult ParallelStep(Commit commit);

        protected abstract void SynchronousStep(TParallelActionResult commit);

        private sealed class BytesToWrite
        {
            public readonly byte[] Bytes;

            public readonly ObjectHash Hash;

            public BytesToWrite(ObjectHash objectHash, byte[] bytes)
            {
                Hash = objectHash;
                Bytes = bytes;
            }
        }

        private sealed class ThreadParams
        {
            public readonly BlockingCollection<BytesToWrite> BytesToWriteCollection;

            public readonly string VcsPath;

            public ThreadParams(string vcsPath, BlockingCollection<BytesToWrite> bytesToWriteCollection)
            {
                VcsPath = vcsPath;
                BytesToWriteCollection = bytesToWriteCollection;
            }
        }
    }
}