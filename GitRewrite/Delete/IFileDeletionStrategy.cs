using System;

namespace GitRewrite.Delete
{
    public interface IFileDeletionStrategy
    {
        bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath);
    }
}