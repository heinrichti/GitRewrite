using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GitRewrite.GitObjects;
using GitRewrite.IO;

namespace GitRewrite
{
    public static class Refs
    {
        public static IEnumerable<Ref> ReadAll(string basePath)
        {
            var packedRefsPath = Path.Combine(basePath, "packed-refs");

            var packedRefsLines = !File.Exists(packedRefsPath)
                ? Enumerable.Empty<Ref>()
                : GetPackedRefs(File.ReadLines(packedRefsPath));

            var refs = GetRefs(basePath, "refs").ToList();

            var refNames = new HashSet<string>();
            foreach (var @ref in refs)
            {
                refNames.Add(@ref.Name);
            }

            return packedRefsLines.Where(x => !refNames.Contains(x.Name)).Union(refs);
        }

        private static IEnumerable<Ref> GetRefs(string basePath, string currentPath)
        {
            var fullPath = Path.Combine(basePath, currentPath);
            foreach (var directory in Directory.GetDirectories(fullPath))
            {
                foreach (var @ref in GetRefs(basePath, currentPath + $"/{Path.GetFileName(directory)}"))
                {
                    if (!@ref.Hash.StartsWith("ref: "))
                        yield return @ref;
                }
            }

            foreach (var file in Directory.GetFiles(fullPath))
            {
                var hash = File.ReadAllText(file).TrimEnd('\n');
                var name = currentPath + "/" + Path.GetFileName(file);

                yield return new Ref(hash, name);
            }
        }

        private static IEnumerable<Ref> GetPackedRefs(IEnumerable<string> lines)
        {
            using (var enumerator = lines.GetEnumerator())
            {
                if (!enumerator.MoveNext())
                    yield break;

                var previousLine = enumerator.Current;
                var lineStarted = !previousLine.StartsWith("#");

                while (enumerator.MoveNext())
                {
                    var currentLine = enumerator.Current;
                    if (currentLine.StartsWith('^'))
                    {
                        yield return new TagRef(previousLine.Substring(0, 40), previousLine.Substring(41), currentLine.Substring(1));
                        lineStarted = false;
                    }
                    else
                    {
                        if (lineStarted)
                            yield return new Ref(previousLine.Substring(0, 40), previousLine.Substring(41));
                        previousLine = currentLine;
                        lineStarted = !currentLine.StartsWith("#");
                    }
                }

                if (lineStarted)
                    yield return new Ref(previousLine.Substring(0, 40), previousLine.Substring(41));
            }
        }

        public static void Update(string basePath, Dictionary<ObjectHash, ObjectHash> rewrittenCommits)
        {
            foreach (var @ref in GetRefs(basePath, "refs"))
            {
                RewriteRef(basePath, @ref.Hash, @ref.Name, rewrittenCommits);
            }

            // Rewrite refs as loose objects and delete packed-refs
            var packedRefsPath = Path.Combine(basePath, "packed-refs");
            if (!File.Exists(packedRefsPath))
                return;

            var content = File.ReadAllLines(packedRefsPath);

            foreach (var line in content)
            {
                if (line[0] == '#' || line[0] == '^')
                    continue;

                RewriteRef(basePath, line.Substring(0, 40), line.Substring(41), rewrittenCommits);
            }

            File.Delete(packedRefsPath);
        }

        private static ObjectHash GetRewrittenCommitHash(ObjectHash hash, Dictionary<ObjectHash, ObjectHash> rewrittenCommits)
        {
            if (rewrittenCommits.TryGetValue(hash, out var rewrittenHash))
            {
                var updatedRef = rewrittenHash;
                while (rewrittenCommits.TryGetValue(rewrittenHash, out rewrittenHash))
                    updatedRef = rewrittenHash;

                return updatedRef;
            }

            return hash;
        }

        private static ObjectHash RewriteRef(string vcsPath, string hash, string refName, Dictionary<ObjectHash, ObjectHash> rewrittenCommits)
        {
            var gitObject = GitObjectFactory.ReadGitObject(vcsPath, new ObjectHash(hash));
            if (gitObject.Type == GitObjectType.Commit)
            {
                var path = Path.Combine(vcsPath, refName);
                var correctedHash = GetRewrittenCommitHash(new ObjectHash(hash), rewrittenCommits);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, correctedHash.ToString());
                return correctedHash;
            }

            if (gitObject.Type == GitObjectType.Tag)
            {
                var tag = (Tag) gitObject;

                var rewrittenObjectHash = tag.PointsToTag
                    ? RewriteRef(vcsPath, tag.Object, "", rewrittenCommits)
                    : GetRewrittenCommitHash(new ObjectHash(tag.Object), rewrittenCommits);

                // points to commit
                var rewrittenTag = tag.WithNewObject(rewrittenObjectHash.ToString());
                HashContent.WriteObject(vcsPath, rewrittenTag);

                var path = Path.Combine(vcsPath, "refs/tags", rewrittenTag.TagName);
                File.WriteAllText(path, rewrittenTag.Hash.ToString());

                return rewrittenTag.Hash;
            }

            throw new NotImplementedException();
        }
    }
}
