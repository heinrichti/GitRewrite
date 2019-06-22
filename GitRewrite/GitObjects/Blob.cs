using System;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Blob : GitObjectBase
    {
        private readonly ReadOnlyMemory<byte> _content;
        public Blob(ObjectHash hash, in ReadOnlyMemory<byte> plainContent) : base(hash, GitObjectType.Blob) => _content = plainContent;

        public string GetContentAsString() => Encoding.UTF8.GetString(_content.Span);

        public override byte[] SerializeToBytes() => _content.ToArray();
    }
}