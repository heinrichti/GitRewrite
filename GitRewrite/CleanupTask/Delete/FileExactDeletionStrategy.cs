using System;
using System.Text;

namespace GitRewrite.CleanupTask.Delete
{
    class FileExactDeletionStrategy : IFileDeletionStrategy
    {
        private readonly ReadOnlyMemory<byte> _fileName;

        public FileExactDeletionStrategy(string fileName) => _fileName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath)
        {
            var len = fileName.Length + currentPath.Length + 1;
            if (len != _fileName.Length)
                return false;

            var span = _fileName.Span;
            return span[currentPath.Length] == (byte) '/' && span.StartsWith(currentPath) && span.EndsWith(fileName);
        }
    }
}
