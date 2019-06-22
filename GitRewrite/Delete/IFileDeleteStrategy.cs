using System;

namespace GitRewrite.Delete
{
    public interface IFileDeleteStrategy
    {
        bool DeleteFile(in ReadOnlySpan<byte> fileName, string currentPath);
    }
}