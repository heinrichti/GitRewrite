using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Commit : GitObjectBase, IEquatable<Commit>
    {
        private readonly Memory<byte> _authorLine;
        private readonly Memory<byte> _commitMessage;
        private readonly Memory<byte> _committerLine;
        private readonly byte[] _content;
        private readonly List<ObjectHash> _parents;
        private readonly Memory<byte> _treeHash;

        private static readonly byte[] TreePrefix = "tree ".Select(x => (byte) x).ToArray();
        private static readonly byte[] ParentPrefix = "parent ".Select(x => (byte) x).ToArray();
        private static readonly byte[] AuthorPrefix = "author ".Select(x => (byte) x).ToArray();
        private static readonly byte[] CommitterPrefix = "committer ".Select(x => (byte) x).ToArray();
        private static readonly byte[] GpgSigPrefix = "gpgsig ".Select(x => (byte) x).ToArray();

        public Commit(ObjectHash hash, byte[] bytes) : base(hash, GitObjectType.Commit)
        {
            _content = bytes;
            var content = bytes.AsMemory();
            _parents = new List<ObjectHash>();

            var nextNewLine = content.Span.IndexOf<byte>(10);
            while (nextNewLine != -1)
            {
                var contentSpan = content.Span;
                if (contentSpan.StartsWith(TreePrefix))
                {
                    _treeHash = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ParentPrefix))
                {
                    _parents.Add(new ObjectHash(content.Span.Slice(7, nextNewLine - 7)));
                }
                else if (content.Span[0] == '\n')
                {
                    _commitMessage = content.Slice(1);
                    break;
                }
                else if (contentSpan.StartsWith(AuthorPrefix))
                {
                    _authorLine = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(CommitterPrefix))
                {
                    _committerLine = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(GpgSigPrefix))
                {
                    // gpgsig are not really handled, instead a gpgsig is not written back when rewriting the object
                    var pgpSignatureEnd = content.Span.IndexOf(PgpSignatureEnd);
                    content = content.Slice(pgpSignatureEnd + PgpSignatureEnd.Length + 1);
                    nextNewLine = content.Span.IndexOf((byte) '\n');
                }
                else
                {
                    throw new Exception("Unknown line");
                }

                content = content.Slice(nextNewLine + 1);
                nextNewLine = content.Span.IndexOf((byte) '\n');
            }
        }

        private static readonly byte[] PgpSignatureEnd = "-----END PGP SIGNATURE-----".Select(c => (byte) c).ToArray();

        public ReadOnlyMemory<byte> CommitterName => GetContributerName(_committerLine.Slice(10));

        public ReadOnlyMemory<byte> AuthorName => GetContributerName(_authorLine.Slice(7));

        private ReadOnlyMemory<byte> GetContributerName(in ReadOnlyMemory<byte> contributerWithTime)
        {
            var span = contributerWithTime.Span;
            int spaces = 0;
            int index = 0;
            for (int i = contributerWithTime.Length - 1; i >= 0; i--)
            {
                if (span[i] == ' ' && ++spaces == 2)
                {
                    index = i;
                    break;
                }
            }

            return contributerWithTime.Slice(0, index);
        }

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

            var author = commit._authorLine;
            var committer = commit._committerLine;

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