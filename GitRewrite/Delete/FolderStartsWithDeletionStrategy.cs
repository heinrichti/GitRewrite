using System;
using System.Text;

namespace GitRewrite.Delete
{
    internal class FolderStartsWithDeletionStrategy : IFolderDeletionStrategy
    {
        private readonly byte[] _startBytes;

        public FolderStartsWithDeletionStrategy(string filePattern) =>
            _startBytes = Encoding.UTF8.GetBytes(filePattern.Substring(0, filePattern.Length - 1));

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) => currentPath.StartsWith(_startBytes);
    }
}