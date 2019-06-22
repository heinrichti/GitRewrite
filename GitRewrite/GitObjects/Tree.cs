using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Tree : GitObjectBase
    {
        public Tree(ObjectHash hash, ReadOnlyMemory<byte> bytes) : base(hash, GitObjectType.Tree)
        {
            var nullTerminatorIndex = IndexOf(bytes.Span, '\0');

            var lines = new List<TreeLine>();

            while (nullTerminatorIndex > 0)
            {
                var textSpan = bytes.Slice(0, nullTerminatorIndex);

                var lineHashInBytes = bytes.Slice(nullTerminatorIndex + 1, 20);

                var objectHash = new ObjectHash(lineHashInBytes.ToArray());

                lines.Add(new TreeLine(textSpan, objectHash));

                bytes = bytes.Slice(nullTerminatorIndex + 21);
                nullTerminatorIndex = IndexOf(bytes.Span, '\0');
            }

            Lines = lines;
        }

        public IReadOnlyList<TreeLine> Lines { get; }

        public static byte[] GetSerializedObject(IReadOnlyList<TreeLine> treeLines)
        {
            // lines bestehen immer aus text + \0 + hash in bytes
            var byteLines = new List<Tuple<ReadOnlyMemory<byte>, byte[]>>();

            foreach (var treeLine in treeLines)
            {
                var textBytes = treeLine.TextBytes;
                var hashBytes = treeLine.Hash.Bytes;
                byteLines.Add(new Tuple<ReadOnlyMemory<byte>, byte[]>(textBytes, hashBytes));
            }

            var bytesTotal = byteLines.Sum(x => x.Item1.Length + 1 + x.Item2.Length);
            var result = new byte[bytesTotal];

            var bytesCopied = 0;

            foreach (var byteLine in byteLines)
            {
                var resultSpan = result.AsSpan(bytesCopied, byteLine.Item1.Length);
                byteLine.Item1.Span.CopyTo(resultSpan);
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

        public static Tree GetFixedTree(IEnumerable<TreeLine> treeLines)
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
            public TreeLine(ReadOnlyMemory<byte> text, ObjectHash hash)
            {
                _text = text;
                Hash = hash;
            }

            private readonly ReadOnlyMemory<byte> _text;

            public string Text => Encoding.UTF8.GetString(_text.Span);

            public ReadOnlyMemory<byte> TextBytes => _text;

            public ObjectHash Hash { get; }

            public bool IsDirectory() => _text.Span[0] != '1';

            public override string ToString() => Hash + " " + Text;
        }
    }
}