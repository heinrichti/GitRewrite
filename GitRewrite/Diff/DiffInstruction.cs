using System;

namespace GitRewrite.Diff
{
    internal interface DiffInstruction
    {
        public int Length { get; }
    }

    internal class AddInstruction : DiffInstruction
    {
        internal readonly Memory<byte> Bytes;
        internal readonly int Start;
        internal readonly int End;

        public AddInstruction(Memory<byte> bytes, ref int currentOffset)
        {
            var bytesToCopy = bytes.Span[currentOffset++];

            Bytes = bytes;
            Start = currentOffset;
            End = currentOffset + bytesToCopy;

            currentOffset += bytesToCopy;
        }

        public AddInstruction(Memory<byte> bytes, int start, int end)
        {
            Bytes = bytes;
            Start = start;
            End = end;
        }

        public int Length => End - Start;
    }

    internal class CopyInstruction : DiffInstruction
    {
        private readonly int _length;
        private readonly int _offset;

        public int Length => _length;
        public int Offset => _offset;

        public CopyInstruction(Span<byte> data, ref int currentOffset)
        {
            var copyInstruction = data[currentOffset++];

            var offset = 0;
            var len = 0;

            if ((copyInstruction & 0b00000001) != 0)
                offset |= data[currentOffset++];

            if ((copyInstruction & 0b00000010) != 0)
                offset |= data[currentOffset++] << 8;

            if ((copyInstruction & 0b00000100) != 0)
                offset |= data[currentOffset++] << 16;

            if ((copyInstruction & 0b00001000) != 0)
                offset |= data[currentOffset++] << 24;

            if ((copyInstruction & 0b00010000) != 0)
                len |= data[currentOffset++];

            if ((copyInstruction & 0b00100000) != 0)
                len |= data[currentOffset++] << 8;

            if ((copyInstruction & 0b01000000) != 0)
                len |= data[currentOffset++] << 16;

            if (len == 0)
                len = 0x10000;

            _length = len;
            _offset = offset;
        }

        internal CopyInstruction(int offset, int length)
        {
            _offset = offset;
            _length = length;
        }
    }
}
