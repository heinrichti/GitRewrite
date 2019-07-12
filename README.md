# GitRewrite
Rewrite git history.

Faster alternative to git filter-branch or bfg-repo-cleaner to perform certain rewrite tasks.
It was tested on windows and linux.

With this tool the repository can be rewritten in a few different ways, like removing deleting files and folders, 
removing empty commits or rewriting committer and author information.

Docker images are available here: https://hub.docker.com/r/lightraven/git-rewrite

[![Build status](https://ci.appveyor.com/api/projects/status/gqdtitbjcd3mquta?svg=true)](https://ci.appveyor.com/project/TimHeinrich/gitrewrite)

## Important notice
This tool will rewrite the git history and therefore change many, if not all, commit hashes.
It will also unsign signed commits. 
Only use it if you fully understand the implications of this!

## Usage
### Deleting files
```cmd
GitRewrite C:/VCS/MyRepo -d file1,file2,file3
GitRewrite C:/VCS/MyRepo --delete-files file1,file2,file3
GitRewrite C:/VCS/MyRepo -d file1,file2,file3 --protect-refs
GitRewrite C:/VCS/MyRepo --delete-files file1,file2,file3 --protect-refs
```
Deleting should be pretty fast, especially when specifying the whole path to the file. 
Simple wildcards for the beginning and the end of the filename are supported, like &ast;.zip.
It also lets you specify the complete path to the file instead of only a file name.
For this the path has to be prefixed by a forward slash and the path seperator also is a forward slash: /path/to/file.txt
Specifying only files with complete path will result in much better performance as not all subtrees have to be checked.

If the goal is to delete files but keep them in all refs (branches and tags) use the --protect-refs flag. 
With this flag GitRewrite will not touch files in a commit a ref points to. 

### Deleting directories
```
GitRewrite -D folder1,folder2,folder3
GitRewrite --delete-directories folder1,folder2,folder3
GitRewrite -D folder1,folder2,folder3 --protect-refs
GitRewrite --delete-directories folder1,folder2,folder3 --protect-refs
```
Patterns and performance characteristics are the same as for deleting files. Can be used in conjunction with -d.

### Remove empty commits
Another useful feature is to remove empty commits. 
For this tool empty commits are defined as commits that have only a single parent and the same tree as their parent.
With git filter-branch this takes days for huge repositories, with GitRewrite it should only be a matter of seconds to minutes.
```
GitRewrite C:/VCS/MyRepo -e
```
This should performa really fast as each commit has to be read only once and written if a parent has changed.

### Rewrite trees with duplicate entries
The main motivation for this tool was a repository where git gc complained about trees having duplicate entries. 
GitRewrite solves this problem by rewriting the trees by removing the duplicates, then rewriting all parent trees, commit and all following commits.
```
GitRewrite C:/VCS/MyRepo --fix-trees
```

### List contributer names
Lists all authors and committers.
```
GitRewrite C:/VCS/MyRepo --contributer-names
```

### Rewrite all contributer names
```
GitRewrite C:/VCS/MyRepo --rewrite-contributers [contributers.txt]
```
Rewrites authors and committers.
The contributers.txt is the mapping from old contributer name to new contributer name:
  Old User \<old@gmail.com> = New User \<new@gmail.com>

### General 
The different actions can only be performed one at a time, for example it is not possible to mix -e and -d.

## Cleanup
After a GitRewrite run files are not actually deleted from the file system. To do this you should run
```
git reflog expire --expire=now && git gc --aggressive
```
Instead of git gc --aggressive you might want to use something faster like git gc --prune=now, while the result may not be as good.

## Important notes
GitRewrite was tested only on a few repository, so there is a big chance that it might fail for you.
Please let me know of any issues or feature requests, I will update the tool when I find the time for it. 
Pull requests very welcome! Still searching for a way to make this even faster, maybe some parallelization options that I have not employed yet or faster file acces (while this should be pretty efficient already using memory mapped files)

## Build instructions
Currently we are building with .NET Core 2.1, so the SDK should be installed.
```
git clone https://github.com/TimHeinrich/GitRewrite.git
cd GitRewrite
dotnet publish --self-contained -r win-x64 -c Release 
```

## Icon attribution
disconnect by Dmitry Baranovskiy from the Noun Project
