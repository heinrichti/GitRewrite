using System;
using System.Text;

namespace GitRewrite.Delete
{
    internal class FolderEndsWithDeletionStrategy : IFolderDeletionStrategy
    {
        private readonly byte[] _endBytes;

        public FolderEndsWithDeletionStrategy(string filePattern) =>
            _endBytes = Encoding.UTF8.GetBytes(filePattern.Substring(1));

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) =>
            currentPath.EndsWith(_endBytes);
    }
}