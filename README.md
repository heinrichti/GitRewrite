# GitRewrite
Faster alternative to git filter-branch or bfg-repo-cleaner to perform certain rewrite tasks.

With this tool the repository can be rewritten in a few different ways. 

## Usage
The most useful is deleting files:
```cmd
GitRewrite C:/VCS/MyRepo -d file1 file2 file3
```
Deleting should be pretty fast, in my tests it even outperformed the bfg repo cleaner by at least factor of 2. 
Simple wildcards for the beginning and the end of the filename are supported, like &ast;.zip.
It also lets you specify the complete path to the file instead of only a file name. 
For this the path has to be prefixed by a forward slash and the path seperator also is a forward slash: /path/to/file.txt

Another useful feature is to remove empty commits. 
For this tool empty commits are defined as commits that have only a single parent and the same tree as their parent.
With git filter-branch this takes days for huge repositories, with GitRewrite it should only be a matter of seconds to minutes.
```
GitRewrite C:/VCS/MyRepo -e
```
This should performa really fast as each commit has to be read only once and written if a parent has changed.

The different actions can only be performed one at a time, for example it is not possible to mix -e and -d.

The main motivation for this tool was a repository where git gc complained about trees having duplicate entries. 
GitRewrite solves this problem by rewriting the trees by removing the duplicates, then rewriting all parent trees, commit and all following commits.
```
GitRewrite C:/VCS/MyRepo --fix-trees
```

## Cleanup
After a GitRewrite run files are not actually deleted. To do this you should run
```
git reflog expire --expire=now && git gc --aggressive
```
Instead of git gc --aggressive you might want to use something faster like git gc --prune=now, while the result will not be as good.

## Important notes
GitRewrite was only tested on one repository, so there is a big chance that it might fail for you. At the moment it will only work on windows because of the zlib library it is using (zlibnet). There may be other issues as well but this is the biggest blocker for letting it run on other systems.
Please let me know of any issues or feature requests, I will update the tool when I find the time for it. Pull requests very welcome! Still searching for a way to make this even faster, maybe some parallelization options that I have not employed yet or faster file acces (while this should be pretty efficient already using memory mapped files)

## Build instructions
Currently we are building with the preview of .NET Core 3.0, so the SDK should be installed.
```
git clone https://github.com/TimHeinrich/GitRewrite.git
cd GitRewrite
dotnet publish --self-contained -r win10-x64 -c Release 
```
