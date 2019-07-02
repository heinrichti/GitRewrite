using System;
using System.Collections.Generic;
using System.Text;

namespace GitRewrite.Delete
{
    public class FolderDeletionStrategies
    {
        private readonly List<IFolderDeletionStrategy> _strategies;

        public readonly List<byte[]> RelevantPaths = new List<byte[]>();

        public FolderDeletionStrategies(IEnumerable<string> patterns)
        {
            _strategies = new List<IFolderDeletionStrategy>();
            foreach (var objectPattern in patterns)
            {
                if (objectPattern[0] == '*')
                    _strategies.Add(new FolderEndsWithDeletionStrategy(objectPattern));
                else if (objectPattern[0] == '/')
                {
                    var bytes = Encoding.UTF8.GetBytes(objectPattern);
                    _strategies.Add(new FolderExactDeletionStrategy(bytes));
                    RelevantPaths.Add(bytes);
                }
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