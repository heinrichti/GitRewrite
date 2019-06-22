using System;
using System.Collections.Generic;

namespace GitRewrite.Delete
{
    public class FileDeleteStrategies
    {
        private readonly List<IFileDeleteStrategy> _strategies;

        public FileDeleteStrategies(IEnumerable<string> filePatterns)
        {
            // TODO currently only simple file name deletion possible, add wildcards and folders
            _strategies = new List<IFileDeleteStrategy>();
            foreach (var filePattern in filePatterns)
            {
                if (filePattern[0] == '*')
                    _strategies.Add(new EndsWithDeletionStrategy(filePattern));
                else if (filePattern[filePattern.Length - 1] == '*')
                    _strategies.Add(new StartsWithDeletionStrategy(filePattern));
                else 
                    _strategies.Add(new SimpleFileDeleteStrategy(filePattern));
            }
        }

        public bool DeleteFile(in ReadOnlySpan<byte> fileName)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.DeleteFile(fileName))
                    return true;
            }

            return false;
        }
    }
}