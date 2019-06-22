using System;
using System.Text;

namespace GitRewrite.Delete
{
    internal class EndsWithDeletionStrategy : IFileDeleteStrategy
    {
        private readonly byte[] _endBytes;

        public EndsWithDeletionStrategy(string filePattern) =>
            _endBytes = Encoding.UTF8.GetBytes(filePattern.Substring(1));

        public bool DeleteFile(in ReadOnlySpan<byte> fileName) => fileName.EndsWith(_endBytes);
    }
}