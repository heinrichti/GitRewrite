using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Tree : GitObjectBase
    {
        public Tree(ObjectHash hash, ReadOnlySpan<byte> bytes) : base(hash, GitObjectType.Tree)
        {
            var nullTerminatorIndex = IndexOf(bytes, '\0');

            var lines = new List<TreeLine>();

            while (nullTerminatorIndex > 0)
            {
                var text = Encoding.UTF8.GetString(bytes.Slice(0, nullTerminatorIndex));

                var lineHashInBytes = bytes.Slice(nullTerminatorIndex + 1, 20);

                var objectHash = new ObjectHash(lineHashInBytes.ToArray());

                lines.Add(new TreeLine(text, objectHash));

                bytes = bytes.Slice(nullTerminatorIndex + 21);
                nullTerminatorIndex = IndexOf(bytes, '\0');
            }

            Lines = lines;
        }

        public IReadOnlyList<TreeLine> Lines { get; }

        public static byte[] GetSerializedObject(IReadOnlyList<TreeLine> treeLines)
        {
            // lines bestehen immer aus text + \0 + hash in bytes
            var byteLines = new List<Tuple<byte[], byte[]>>();

            foreach (var treeLine in treeLines)
            {
                var textBytes = Encoding.UTF8.GetBytes(treeLine.Text);
                var hashBytes = treeLine.Hash.Bytes;
                byteLines.Add(new Tuple<byte[], byte[]>(textBytes, hashBytes));
            }

            var bytesTotal = byteLines.Sum(x => x.Item1.Length + 1 + x.Item2.Length);
            var result = new byte[bytesTotal];

            var bytesCopied = 0;

            foreach (var byteLine in byteLines)
            {
                Array.Copy(byteLine.Item1, 0, result, bytesCopied, byteLine.Item1.Length);
                bytesCopied += byteLine.Item1.Length;
                result[bytesCopied++] = 0;
                Array.Copy(byteLine.Item2, 0, result, bytesCopied, 20);
                bytesCopied += 20;
            }

            return result;
        }

        public IEnumerable<TreeLine> GetDirectories() => Lines.Where(line => line.IsDirectory());

        public static bool HasDuplicateLines(IReadOnlyList<TreeLine> treeLines)
        {
            var lines = new HashSet<string>();
            return !treeLines.All(x => lines.Add(x.Text));
        }

        public static Tree GetFixedTree(IReadOnlyList<TreeLine> treeLines)
        {
            var distinctTreeLines = new List<TreeLine>();
            var hashSet = new HashSet<string>();

            foreach (var treeLine in treeLines)
                if (hashSet.Add(treeLine.Text))
                    distinctTreeLines.Add(treeLine);

            var serializedObject = GetSerializedObject(distinctTreeLines);

            return GitObjectFactory.TreeFromContentBytes(serializedObject);
        }

        public override byte[] SerializeToBytes() => GetSerializedObject(Lines);

        public class TreeLine
        {
            public TreeLine(string text, ObjectHash hash)
            {
                Text = text;
                Hash = hash;
            }

            public string Text { get; }
            public ObjectHash Hash { get; }

            public bool IsDirectory() => Text[0] != '1';
        }
    }
}