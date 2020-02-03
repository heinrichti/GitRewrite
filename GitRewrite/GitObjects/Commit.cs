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

        public Commit(ObjectHash hash, byte[] bytes) : base(hash, GitObjectType.Commit)
        {
            _content = bytes;
            var content = bytes.AsMemory();
            _parents = new List<ObjectHash>();

            var nextNewLine = content.Span.IndexOf((byte) '\n');
            while (nextNewLine != -1)
            {
                var contentSpan = content.Span;
                if (contentSpan.StartsWith(ObjectPrefixes.TreePrefix))
                {
                    _treeHash = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.ParentPrefix))
                {
                    _parents.Add(new ObjectHash(content.Span.Slice(7, nextNewLine - 7)));
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.AuthorPrefix))
                {
                    _authorLine = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.CommitterPrefix))
                {
                    _committerLine = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.GpgSigPrefix))
                {
                    // gpgsig are not really handled, instead a gpgsig is not written back when rewriting the object
                    var pgpSignatureEnd = content.Span.IndexOf(PgpSignatureEnd);
                    content = content.Slice(pgpSignatureEnd + PgpSignatureEnd.Length + 1);
                    nextNewLine = content.Span.IndexOf((byte) '\n');
                }
                else
                {
                    // We view everything that is not defined above as commit message
                    _commitMessage = content;
                    break;
                }

                content = content.Slice(nextNewLine + 1);
                nextNewLine = content.Span.IndexOf((byte) '\n');
            }
        }

        private static readonly byte[] PgpSignatureEnd = "-----END PGP SIGNATURE-----".Select(c => (byte) c).ToArray();

        public ReadOnlySpan<byte> GetCommitterBytes() => GetContributorName(_committerLine.Slice(10));

        public string GetCommitterName() => Encoding.UTF8.GetString(GetCommitterBytes());

        public ReadOnlySpan<byte> GetAuthorBytes() => GetContributorName(_authorLine.Slice(7));

        public string GetAuthorName() => Encoding.UTF8.GetString(GetAuthorBytes());

        private ReadOnlySpan<byte> GetContributorName(in ReadOnlyMemory<byte> contributorWithTime)
        {
            var span = contributorWithTime.Span;
            int spaces = 0;
            int index = 0;
            for (int i = contributorWithTime.Length - 1; i >= 0; i--)
            {
                if (span[i] == ' ' && ++spaces == 2)
                {
                    index = i;
                    break;
                }
            }

            return contributorWithTime.Span.Slice(0, index);
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
            List<ObjectHash> parents)
        {
            const int firstLineLength = 46;
            const int treePrefixLength = 5;
            const int parentLineLength = 7 + 40 + 1;

            var contentSize = firstLineLength + parents.Count * parentLineLength + commit._authorLine.Length + 1 +
                              commit._committerLine.Length + 1 + commit._commitMessage.Length;

            var resultBuffer = new byte[contentSize];

            Array.Copy(ObjectPrefixes.TreePrefix, resultBuffer, treePrefixLength);
            Array.Copy(treeHash.ToStringBytes(), 0, resultBuffer, treePrefixLength, 40);
            resultBuffer[45] = (byte) '\n';

            var bytesCopied = firstLineLength;

            foreach (var parent in parents)
            {
                Array.Copy(ObjectPrefixes.ParentPrefix, 0, resultBuffer, bytesCopied, 7);
                Array.Copy(parent.ToStringBytes(), 0, resultBuffer, bytesCopied + 7, 40);
                bytesCopied += 47;
                resultBuffer[bytesCopied++] = (byte) '\n';
            }

            commit._authorLine.Span.CopyTo(resultBuffer.AsSpan(bytesCopied, commit._authorLine.Length));
            bytesCopied += commit._authorLine.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            commit._committerLine.Span.CopyTo(resultBuffer.AsSpan(bytesCopied, commit._committerLine.Length));
            bytesCopied += commit._committerLine.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            commit._commitMessage.Span.CopyTo(resultBuffer.AsSpan(bytesCopied, commit._commitMessage.Length));

            return resultBuffer;
        }

        public byte[] WithChangedContributor(Dictionary<string, string> contributorMapping, IEnumerable<ObjectHash> parents)
        {
            const int firstLineLength = 46;
            const int parentLineLength = 7 + 40 + 1;

            var author = GetAuthorName();
            var committer = GetCommitterName();
            if (!contributorMapping.TryGetValue(author, out var newAuthor))
                newAuthor = author;

            if (!contributorMapping.TryGetValue(this.GetCommitterName(), out var newCommitter))
                newCommitter = committer;

            var authorLine = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(this._authorLine.Span).Replace(author, newAuthor));
            var committerLine = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(this._committerLine.Span).Replace(committer, newCommitter));

            var contentSize = firstLineLength + _parents.Count * parentLineLength + authorLine.Length + 1 +
                              committerLine.Length + 1 + _commitMessage.Length;

            var resultBuffer = new byte[contentSize];

            _treeHash.Span.CopyTo(resultBuffer.AsSpan(0, this._treeHash.Length));
            resultBuffer[45] = (byte) '\n';

            var bytesCopied = firstLineLength;

            foreach (var parent in parents)
            {
                Array.Copy(ObjectPrefixes.ParentPrefix, 0, resultBuffer, bytesCopied, 7);
                Array.Copy(parent.ToStringBytes(), 0, resultBuffer, bytesCopied + 7, 40);
                bytesCopied += 47;
                resultBuffer[bytesCopied++] = (byte) '\n';
            }

            Array.Copy(authorLine, 0, resultBuffer, bytesCopied, authorLine.Length);
            bytesCopied += authorLine.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            Array.Copy(committerLine, 0, resultBuffer, bytesCopied, committerLine.Length);
            //commit._committerLine.Span.CopyTo(resultBuffer.AsSpan(bytesCopied, commit._committerLine.Length));
            bytesCopied += committerLine.Length;
            resultBuffer[bytesCopied++] = (byte) '\n';

            _commitMessage.Span.CopyTo(resultBuffer.AsSpan(bytesCopied, _commitMessage.Length));

            return resultBuffer;
        }


        public override bool Equals(object obj) => ReferenceEquals(this, obj) || obj is Commit other && Equals(other);

        public override int GetHashCode() => base.GetHashCode();
    }
}