using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Commit : GitObjectBase, IEquatable<Commit>
    {
        private readonly Memory<byte> _author;
        private readonly Memory<byte> _commitMessage;
        private readonly Memory<byte> _committer;
        private readonly byte[] _content;
        private readonly List<ObjectHash> _parents;
        private readonly Memory<byte> _treeHash;

        public Commit(ObjectHash hash, byte[] bytes) : base(hash, GitObjectType.Commit)
        {
            _content = bytes;
            var content = bytes.AsMemory();
            _parents = new List<ObjectHash>();

            var nextNewLine = content.Span.IndexOf<byte>(10);
            while (nextNewLine != -1)
            {
                if (StartsWith(content, "tree "))
                {
                    _treeHash = content.Slice(0, nextNewLine);
                }
                else if (StartsWith(content, "parent "))
                {
                    _parents.Add(new ObjectHash(content.Span.Slice(7, nextNewLine - 7)));
                }
                else if (content.Span[0] == '\n')
                {
                    _commitMessage = content.Slice(1);
                    break;
                }
                else if (StartsWith(content, "author "))
                {
                    _author = content.Slice(0, nextNewLine);
                }
                else if (StartsWith(content, "committer "))
                {
                    _committer = content.Slice(0, nextNewLine);
                }
                else
                {
                    throw new Exception("Unknown line");
                }

                content = content.Slice(nextNewLine + 1);
                nextNewLine = content.Span.IndexOf((byte) '\n');
            }
        }

        public string Committer => Encoding.UTF8.GetString(_committer.Span.Slice(10));

        public string Author => Encoding.UTF8.GetString(_author.Span.Slice(7));

        public ObjectHash TreeHash => new ObjectHash(_treeHash.Span.Slice(5));

        public List<ObjectHash> Parents => _parents;

        public string CommitMessage => Encoding.UTF8.GetString(_commitMessage.Span);

        public bool HasParents => _parents.Any();

        public bool Equals(Commit other)
        {
            if (ReferenceEquals(null, other)) return false;

            if (ReferenceEquals(this, other)) return true;

            return base.Equals(other) && Hash.Equals(other.Hash);
        }

        public override byte[] SerializeToBytes()
            => _content;

        private static readonly byte[] TreePrefix = {(byte) 't', (byte) 'r', (byte) 'e', (byte) 'e', (byte) ' '};

        public static byte[] GetSerializedCommitWithChangedTreeAndParents(Commit commit, ObjectHash treeHash,
            IEnumerable<ObjectHash> parents)
        {
            var tree = new byte[45];
            Array.Copy(TreePrefix, tree, 5);
            var hashString = treeHash.ToString();
            for (int i = 0; i < hashString.Length; i++)
            {
                tree[i + 5] = (byte)hashString[i];
            }

            var parentLines = new List<byte[]>();
            foreach (var commitParent in parents) 
                parentLines.Add(Encoding.UTF8.GetBytes("parent " + commitParent));

            var author = commit._author;
            var committer = commit._committer;

            var message = commit._commitMessage;

            var contentSize = tree.Length + 1 + parentLines.Sum(x => x.Length + 1) + author.Length + 1 +
                              committer.Length + 1 + message.Length + 1;

            var resultBuffer = new byte[contentSize];

            Array.Copy(tree, 0, resultBuffer, 0, tree.Length);
            var bytesCopied = tree.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            foreach (var parentLine in parentLines)
            {
                Array.Copy(parentLine, 0, resultBuffer, bytesCopied, parentLine.Length);
                bytesCopied += parentLine.Length;
                resultBuffer[bytesCopied++] = (byte) '\n';
            }

            SpanToArrayCopy(author.Span, resultBuffer, bytesCopied);
            bytesCopied += author.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            SpanToArrayCopy(committer.Span, resultBuffer, bytesCopied);
            bytesCopied += committer.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';
            resultBuffer[bytesCopied++] = (byte) '\n';

            SpanToArrayCopy(message.Span, resultBuffer, bytesCopied);

            return resultBuffer;
        }

        private static void SpanToArrayCopy<T>(in ReadOnlySpan<T> source, T[] destination, int destinationIndex)
        {
            for (var i = 0; i < source.Length; i++) destination[i + destinationIndex] = source[i];
        }

        public override bool Equals(object obj) => ReferenceEquals(this, obj) || obj is Commit other && Equals(other);

        public override int GetHashCode() => base.GetHashCode();
    }
}