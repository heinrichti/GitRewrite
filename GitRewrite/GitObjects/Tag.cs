using System;
using System.Linq;
using System.Text;

namespace GitRewrite.GitObjects
{
    public sealed class Tag : GitObjectBase
    {
        private static readonly byte[] TagKey = "tag".Select(x => (byte) x).ToArray();
        private static readonly byte[] TreeKey = "tree".Select(x => (byte) x).ToArray();
        private readonly Memory<byte> _content;
        private readonly Memory<byte> _message;
        private readonly Memory<byte> _object;
        private readonly Memory<byte> _tag;
        private readonly Memory<byte> _tagger;
        private readonly Memory<byte> _type;

        public Tag(ObjectHash hash, Memory<byte> content) : base(hash, GitObjectType.Tag)
        {
            _content = content;

            var nextNewLine = content.Span.IndexOf<byte>(10);
            while (nextNewLine != -1)
            {
                var contentSpan = content.Span;
                if (contentSpan.StartsWith(ObjectPrefixes.ObjectPrefix))
                {
                    _object = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.TypePrefix))
                {
                    _type = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.TagPrefix))
                {
                    _tag = content.Slice(0, nextNewLine);
                }
                else if (contentSpan.StartsWith(ObjectPrefixes.TaggerPrefix))
                {
                    _tagger = content.Slice(0, nextNewLine);
                }
                else
                {
                    _message = content;
                    break;
                }

                content = content.Slice(nextNewLine + 1);
                nextNewLine = content.Span.IndexOf<byte>(10);
            }
        }

        public string Object => Encoding.UTF8.GetString(_object.Span.Slice(7));
        public string TypeName => Encoding.UTF8.GetString(_type.Span.Slice(5));
        public string TagName => Encoding.UTF8.GetString(_tag.Span.Slice(4));
        public string Tagger => Encoding.UTF8.GetString(_tagger.Span.Slice(7));
        public string Message => Encoding.UTF8.GetString(_message.Span);

        public bool PointsToTag => _type.Span.Slice(5).StartsWith(TagKey);
        public bool PointsToTree => _type.Span.Slice(5).StartsWith(TreeKey);

        public Tag WithNewObject(string obj)
        {
            var resultBuffer = new byte[_content.Length];
            var resultIndex = 0;

            for (var i = 0; i < 7; i++)
                resultBuffer[resultIndex++] = _object.Span[i];

            var objBytes = Encoding.ASCII.GetBytes(obj);
            for (var i = 0; i < objBytes.Length; i++)
                resultBuffer[resultIndex++] = objBytes[i];

            resultBuffer[resultIndex++] = 10;

            for (var i = 0; i < _type.Length; i++)
                resultBuffer[resultIndex++] = _type.Span[i];

            resultBuffer[resultIndex++] = 10;

            for (var i = 0; i < _tag.Length; i++)
                resultBuffer[resultIndex++] = _tag.Span[i];

            resultBuffer[resultIndex++] = 10;

            for (var i = 0; i < _tagger.Length; i++)
                resultBuffer[resultIndex++] = _tagger.Span[i];

            resultBuffer[resultIndex++] = 10;

            for (var i = 0; i < _message.Length; i++)
                resultBuffer[resultIndex++] = _message.Span[i];

            return GitObjectFactory.TagFromContentBytes(resultBuffer);
        }

        public override byte[] SerializeToBytes() => _content.ToArray();
    }
}