using System;

namespace GitRewrite.CleanupTask.Delete
{
    public interface IFileDeletionStrategy
    {
        bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath);
    }
}