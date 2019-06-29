﻿using System;

namespace GitRewrite.Delete
{
    public interface IFolderDeletionStrategy
    {
        bool DeleteObject(in ReadOnlySpan<byte> currentPath);
    }
}