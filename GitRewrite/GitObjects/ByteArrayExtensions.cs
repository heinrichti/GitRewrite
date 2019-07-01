using System;

namespace GitRewrite.GitObjects
{
    static class ByteArrayExtensions
    {
        public static bool SequenceEquals(this byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length)
                return false;

            for (int i = b1.Length - 1; i >= 0; i--)
            {
                if (b1[i] != b2[i])
                    return false;
            }

            return true;
        }

        public static bool SpanEquals(this in Span<byte> span1, in ReadOnlySpan<byte> span2)
        {
            if (span1.Length != span2.Length)
                return false;

            for (int i = span1.Length - 1; i >= 0; i--)
            {
                if (span1[i] != span2[i])
                    return false;
            }

            return true;
        }

        public static bool SpanEquals(this in ReadOnlySpan<byte> span1, in ReadOnlySpan<byte> span2)
        {
            if (span1.Length != span2.Length)
                return false;

            for (int i = span1.Length - 1; i >= 0; i--)
            {
                if (span1[i] != span2[i])
                    return false;
            }

            return true;
        }
    }
}
