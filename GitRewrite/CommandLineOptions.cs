using System;
using System.Collections.Generic;
using System.Linq;

namespace GitRewrite
{
    internal class CommandLineOptions
    {
        private CommandLineOptions()
        {
        }

        public bool ListContributorNames { get; private set; }

        public bool ShowHelp { get; private set; }

        public string RepositoryPath { get; private set; }

        public List<string> FilesToDelete { get; } = new List<string>();

        public List<string> FoldersToDelete { get; } = new List<string>();

        public bool FixTrees { get; private set; }

        public bool RemoveEmptyCommits { get; private set; }

        public string ContributorMappingFile { get; private set; }

        public bool ProtectRefs { get; private set; }

        internal static bool TryParse(string[] args, out CommandLineOptions options)
        {
            options = new CommandLineOptions();
            var deleteFilesStarted = false;
            var deleteFoldersStarted = false;
            var rewriteContributorsFileExpected = false;

            foreach (var arg in args)
                if (deleteFilesStarted)
                {
                    options.FilesToDelete.AddRange(GetFiles(arg));
                    deleteFilesStarted = false;
                }
                else if (deleteFoldersStarted)
                {
                    options.FoldersToDelete.AddRange(GetFiles(arg));
                    deleteFoldersStarted = false;
                }
                else if (rewriteContributorsFileExpected)
                {
                    options.ContributorMappingFile = arg;
                    rewriteContributorsFileExpected = false;
                }
                else
                {
                    switch (arg)
                    {
                        case "-e":
                            options.RemoveEmptyCommits = true;
                            break;
                        case "-d":
                        case "--delete-files":
                            deleteFilesStarted = true;
                            break;
                        case "-D":
                        case "--delete-folders":
                            deleteFoldersStarted = true;
                            break;
                        case "--fix-trees":
                            options.FixTrees = true;
                            break;
                        case "-h":
                        case "--help":
                            options.ShowHelp = true;
                            break;
                        case "--contributor-names":
                            options.ListContributorNames = true;
                            break;
                        case "--rewrite-contributors":
                            rewriteContributorsFileExpected = true;
                            break;
                        case "--protect-refs":
                            options.ProtectRefs = true;
                            break;
                        default:
                            if (arg.StartsWith("-"))
                                throw new ArgumentException("Could not parse arguments.");

                            if (!string.IsNullOrWhiteSpace(options.RepositoryPath))
                                throw new ArgumentException("Repository path is multiple times.");

                            options.RepositoryPath = arg;

                            break;
                    }
                }

            var optionsSet = 0;
            optionsSet += options.FixTrees ? 1 : 0;
            optionsSet += options.FilesToDelete.Any() || options.FoldersToDelete.Any() ? 1 : 0;
            optionsSet += options.RemoveEmptyCommits ? 1 : 0;
            optionsSet += options.ListContributorNames ? 1 : 0;
            optionsSet += !string.IsNullOrWhiteSpace(options.ContributorMappingFile) ? 1 : 0;

            if (optionsSet > 1)
            {
                Console.WriteLine(
                    "Cannot mix operations. Only choose one operation at a time (multiple file deletes are allowed).");
                Console.WriteLine();
                PrintHelp();
                return false;
            }

            if (optionsSet == 0 || string.IsNullOrWhiteSpace(options.RepositoryPath))
            {
                PrintHelp();
                return false;
            }

            return true;
        }

        public static void PrintHelp()
        {
            Console.WriteLine("GitRewrite [options] repository_path");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("-e");
            Console.WriteLine("  Removes empty commits from the repository.");
            Console.WriteLine();

            Console.WriteLine("-d [filePattern...], --delete-files [filePattern...]");
            Console.WriteLine("  Delete files from the repository.");
            Console.WriteLine(
                "  [filePattern...] is a list of comma separated patterns. Option can be defined multiple times.");
            Console.WriteLine(
                "  If filePattern is filename, then the file with the name filename will be deleted from all directories.");
            Console.WriteLine(
                "  If filePattern is filename*, then all files starting with filename will be deleted from all directories.");
            Console.WriteLine(
                "  If filePattern is *filename, then all files ending with filename will be deleted from all directories.");
            Console.WriteLine(
                "  If filePattern is /path/to/filename, then the file will be delete only in the exact directory.");
                Console.WriteLine("  Use --protect-refs to not update commits refs point to.");
            Console.WriteLine();

            Console.WriteLine("-D [directoryPattern...], --delete-directories [directoryPattern...]");
            Console.WriteLine(
                "  Delete whole directories from the repository. Directory specifications follow the same pattern as for files.");
            Console.WriteLine("  Can be combined with deleting files.");
            Console.WriteLine();

            Console.WriteLine("--rewrite-contributors [contributors.txt]");
            Console.WriteLine("  Rewrite author and committer information.");
            Console.WriteLine("  contributors.txt is the mapping file for the names that should be replaced. Each line represents one contributor to replace.");
            Console.WriteLine("  Format is ");
            Console.WriteLine("      Test User <test@user.com> = New Test User <newtest@user.comm>");
            Console.WriteLine();

            Console.WriteLine("--contributor-names");
            Console.WriteLine("  Writes all authors and committers to stdout");
            Console.WriteLine();

            Console.WriteLine("--fix-trees");
            Console.WriteLine(
                "  Checks for trees with duplicate entries. Rewrites the tree taking only the first entry.");
            Console.WriteLine();
        }

        private static List<string> GetFiles(string fileString)
        {
            var result = new List<string>();

            var fileSpan = fileString.AsSpan();

            var indexOfSeperator = fileSpan.IndexOf(',');
            while (indexOfSeperator >= 0)
            {
                if (indexOfSeperator == 0)
                    continue;

                var arg = new string(fileSpan.Slice(0, indexOfSeperator));
                if (!string.IsNullOrWhiteSpace(arg))
                    result.Add(arg);
                fileSpan = fileSpan.Slice(indexOfSeperator + 1);
                indexOfSeperator = fileSpan.IndexOf(',');
            }

            var lastArg = new string(fileSpan);
            if (!string.IsNullOrWhiteSpace(lastArg))
                result.Add(new string(fileSpan));

            return result;
        }
    }
}
