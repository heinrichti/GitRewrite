using System;
using System.Text;

namespace GitRewrite.CleanupTask.Delete
{
    internal class FileEndsWithDeletionStrategy : IFileDeletionStrategy
    {
        private readonly byte[] _endBytes;

        public FileEndsWithDeletionStrategy(string filePattern) =>
            _endBytes = Encoding.UTF8.GetBytes(filePattern.Substring(1));

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath) =>
            fileName.EndsWith(_endBytes);
    }
}