using System;
using System.Text;
using GitRewrite.GitObjects;

namespace GitRewrite.Delete
{
    class FolderExactDeletionStrategy : IFolderDeletionStrategy
    {
        private readonly Memory<byte> _folderName;

        public FolderExactDeletionStrategy(string fileName) => _folderName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) => _folderName.Span.SpanEquals(currentPath);
    }
}
