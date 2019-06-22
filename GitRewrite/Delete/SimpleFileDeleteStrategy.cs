using System;
using System.Text;

namespace GitRewrite.Delete
{
    public class SimpleFileDeleteStrategy : IFileDeleteStrategy
    {
        private readonly byte[] _fileName;

        public SimpleFileDeleteStrategy(string fileName) => _fileName = Encoding.UTF8.GetBytes(fileName);

        public bool DeleteFile(in ReadOnlySpan<byte> fileName, string currentPath)
            => fileName.SequenceEqual(_fileName);
    }
}