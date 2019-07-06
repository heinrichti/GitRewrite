using System;
using System.Text;

namespace GitRewrite.CleanupTask.Delete
{
    internal class FileStartsWithDeletionStrategy : IFileDeletionStrategy
    {
        private readonly byte[] _startBytes;

        public FileStartsWithDeletionStrategy(string filePattern) =>
            _startBytes = Encoding.UTF8.GetBytes(filePattern.Substring(0, filePattern.Length - 1));

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath) => fileName.StartsWith(_startBytes);
    }
}