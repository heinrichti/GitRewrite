namespace GitRewrite.IO
{
    public readonly struct PackObject
    {
        public PackObject(int type, long offset, int headerLength, int dataSize)
        {
            Type = type;
            Offset = offset;
            HeaderLength = headerLength;
            DataSize = dataSize;
        }

        public readonly int Type;
        public readonly long Offset;
        public readonly int HeaderLength;
        public readonly int DataSize;
    }
}