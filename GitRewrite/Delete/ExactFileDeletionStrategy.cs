using System;
using System.Text;

namespace GitRewrite.Delete
{
    class ExactFileDeletionStrategy : IFileDeleteStrategy
    {
        private readonly string _fileName;

        public ExactFileDeletionStrategy(string fileName) => _fileName = fileName;

        public bool DeleteFile(in ReadOnlySpan<byte> fileName, string currentPath)
            => currentPath + "/" + Encoding.UTF8.GetString(fileName) == _fileName;
    }
}
