using System;
using System.Text;

namespace GitRewrite.Delete
{
    public class FolderSimpleDeleteStrategy : IFolderDeletionStrategy
    {
        private readonly byte[] _fileName;

        public FolderSimpleDeleteStrategy(string fileName) => _fileName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteObject(in ReadOnlySpan<byte> currentPath) =>
            currentPath.Length > _fileName.Length &&
            currentPath[currentPath.Length - _fileName.Length - 1] == (byte) '/' && currentPath.EndsWith(_fileName);
    }
}