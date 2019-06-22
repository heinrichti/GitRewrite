# GitRewrite
Faster alternative to git filter-branch or bfg-repo-cleaner to perform certain rewrite tasks.

With this tool the repository can be rewritten in a few different ways. The most useful is deleting files:
```cmd
GitRewrite C:/VCS/MyRepo -d file1 file2 file3
```
Deleting should be pretty fast, in my tests it even outperformed the bfg repo cleaner by a factor of 2. It has no wildcard support yet.

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
