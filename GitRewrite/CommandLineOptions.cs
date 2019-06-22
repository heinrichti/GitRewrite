using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace GitRewrite
{
    class CommandLineOptions
    {
        [Value(0, MetaName = "RepositoryPath", HelpText = "Path to the git repository. Should be cloned with --mirror. This has to be the first parameter", Required = true)]
        public string RepositoryPath { get; set; }

        [Option('d', "delete-file", HelpText = "Deletes the given files from the repository. Files are separated by a single space")]
        public IEnumerable<string> FilesToDelete { get; set; }

        [Option("fix-trees", HelpText = "Fixes trees with duplicate entries by only taking the first entry with the same name.")]
        public bool FixTrees { get; set; }

        [Option('e', "remove-empty", HelpText = "Removes empty commits.")]
        public bool RemoveEmptyCommits { get; set; }

        
    }
}
