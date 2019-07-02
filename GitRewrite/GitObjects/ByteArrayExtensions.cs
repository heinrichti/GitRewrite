using System;

namespace GitRewrite.GitObjects
{
    static class ByteArrayExtensions
    {
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
