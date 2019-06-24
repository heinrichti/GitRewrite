using System;
using System.Collections.Generic;

namespace GitRewrite.Delete
{
    public class FileDeleteStrategies
    {
        private readonly List<IFileDeleteStrategy> _strategies;

        public FileDeleteStrategies(IEnumerable<string> filePatterns)
        {
            _strategies = new List<IFileDeleteStrategy>();
            foreach (var filePattern in filePatterns)
            {
                if (filePattern[0] == '*')
                    _strategies.Add(new EndsWithDeletionStrategy(filePattern));
                else if (filePattern[0] == '/')
                    _strategies.Add(new ExactFileDeletionStrategy(filePattern));
                else if (filePattern[filePattern.Length - 1] == '*')
                    _strategies.Add(new StartsWithDeletionStrategy(filePattern));
                else 
                    _strategies.Add(new SimpleFileDeleteStrategy(filePattern));
            }
        }

        public bool DeleteFile(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.DeleteFile(fileName, currentPath))
                    return true;
            }

            return false;
        }
    }
}