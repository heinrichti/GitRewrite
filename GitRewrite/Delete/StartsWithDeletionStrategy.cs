using System;
using System.Text;

namespace GitRewrite.Delete
{
    internal class StartsWithDeletionStrategy : IFileDeleteStrategy
    {
        private readonly byte[] _startBytes;

        public StartsWithDeletionStrategy(string filePattern) =>
            _startBytes = Encoding.UTF8.GetBytes(filePattern.Substring(0, filePattern.Length - 1));

        public bool DeleteFile(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath) => fileName.StartsWith(_startBytes);
    }
}