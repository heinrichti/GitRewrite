using System;
using System.Collections.Generic;

namespace GitRewrite.Delete
{
    public class FolderDeletionStrategies
    {
        private readonly List<IFolderDeletionStrategy> _strategies;

        public FolderDeletionStrategies(IEnumerable<string> patterns)
        {
            _strategies = new List<IFolderDeletionStrategy>();
            foreach (var objectPattern in patterns)
            {
                if (objectPattern[0] == '*')
                    _strategies.Add(new FolderEndsWithDeletionStrategy(objectPattern));
                else if (objectPattern[0] == '/')
                    _strategies.Add(new FolderExactDeletionStrategy(objectPattern));
                else if (objectPattern[objectPattern.Length - 1] == '*')
                    _strategies.Add(new FolderStartsWithDeletionStrategy(objectPattern));
                else 
                    _strategies.Add(new FolderSimpleDeleteStrategy(objectPattern));
            }
        }

        public bool DeleteObject(ReadOnlySpan<byte> currentPath)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.DeleteObject(currentPath))
                    return true;
            }

            return false;
        }
    }
}