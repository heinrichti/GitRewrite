using System;
using System.Text;
using GitRewrite.GitObjects;

namespace GitRewrite.Delete
{
    class FolderExactDeletionStrategy : IFolderDeletionStrategy
    {
        private readonly Memory<byte> _folderName;

        public FolderExactDeletionStrategy(byte[] fileName) => _folderName = fileName;

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) => _folderName.Span.SpanEquals(currentPath);
    }
}
