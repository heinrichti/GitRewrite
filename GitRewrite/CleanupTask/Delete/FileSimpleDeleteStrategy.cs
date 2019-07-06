using System;
using System.Text;
using GitRewrite.GitObjects;

namespace GitRewrite.CleanupTask.Delete
{
    public class FileSimpleDeleteStrategy : IFileDeletionStrategy
    {
        private readonly byte[] _fileName;

        public FileSimpleDeleteStrategy(string fileName) => _fileName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath) =>
            fileName.SpanEquals(_fileName);
    }
}