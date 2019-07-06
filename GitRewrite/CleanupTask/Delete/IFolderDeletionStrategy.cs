using System;

namespace GitRewrite.CleanupTask.Delete
{
    public interface IFolderDeletionStrategy
    {
        bool DeleteObject(in ReadOnlySpan<byte> currentPath);
    }
}