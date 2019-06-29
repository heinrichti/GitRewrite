using System;
using System.Text;

namespace GitRewrite.Delete
{
    class FolderExactDeletionStrategy : IFolderDeletionStrategy
    {
        private readonly ReadOnlyMemory<byte> _fileName;

        public FolderExactDeletionStrategy(string fileName) => _fileName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) => _fileName.Span.SequenceEqual(currentPath);
    }
}
