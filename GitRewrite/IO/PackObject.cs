namespace GitRewrite.IO
{
    public class PackObject
    {
        public PackObject(int type, long offset, int headerLength, int dataSize)
        {
            Type = type;
            Offset = offset;
            HeaderLength = headerLength;
            DataSize = dataSize;
        }

        public int Type { get; }
        public long Offset { get; }
        public int HeaderLength { get; }
        public int DataSize { get; }
    }
}