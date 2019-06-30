using System;
using System.Collections.Generic;
using System.Text;

namespace GitRewrite.Delete
{
    public class FileDeletionStrategies
    {
        private readonly List<IFileDeletionStrategy> _strategies;

        public FileDeletionStrategies(IEnumerable<string> filePatterns)
        {
            _strategies = new List<IFileDeletionStrategy>();
            foreach (var objectPattern in filePatterns)
            {
                if (objectPattern[0] == '*')
                    _strategies.Add(new FileEndsWithDeletionStrategy(objectPattern));
                else if (objectPattern[0] == '/')
                {
                    _strategies.Add(new FileExactDeletionStrategy(objectPattern));

                    var indexToCut = objectPattern.LastIndexOf('/');
                    var pathString = objectPattern.Substring(0, indexToCut);
                    var bytes = Encoding.UTF8.GetBytes(pathString);
                    RelevantPaths.Add(bytes);
                }
                else if (objectPattern[objectPattern.Length - 1] == '*')
                    _strategies.Add(new FileStartsWithDeletionStrategy(objectPattern));
                else 
                    _strategies.Add(new FileSimpleDeleteStrategy(objectPattern));
            }
        }

        public List<byte[]> RelevantPaths { get; } = new List<byte[]>();

        public bool DeleteObject(in ReadOnlySpan<byte> fileName, ReadOnlySpan<byte> currentPath)
        {
            foreach (var strategy in _strategies)
            {
                if (strategy.DeleteObject(fileName, currentPath))
                    return true;
            }

            return false;
        }
    }
}