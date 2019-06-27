using System;
using System.Collections.Generic;

namespace GitRewrite
{
    internal class CommandLineOptions
    {
        public CommandLineOptions(string[] args)
        {
            var deleteFilesStarted = false;

            foreach (var arg in args)
                if (deleteFilesStarted)
                {
                    FilesToDelete.AddRange(GetFiles(arg));
                    deleteFilesStarted = false;
                }
                else switch (arg)
                {
                    case "-e":
                        RemoveEmptyCommits = true;
                        break;
                    case "-d":
                    case "--delete-files":
                        deleteFilesStarted = true;
                        break;
                    case "--fix-trees":
                        FixTrees = true;
                        break;
                    case "-h":
                    case "--help":
                        ShowHelp = true;
                        break;
                    case "--contributer-names":
                        ListContributerNames = true;
                        break;
                    default:
                        if (arg.StartsWith("-"))
                            throw new ArgumentException("Could not parse arguments.");
                        RepositoryPath = arg;
                        break;
                }
        }

        public bool ListContributerNames { get; }

        public bool ShowHelp { get; }

        public string RepositoryPath { get; }

        public List<string> FilesToDelete { get; } = new List<string>();

        public bool FixTrees { get; }

        public bool RemoveEmptyCommits { get; }

        private List<string> GetFiles(string fileString)
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