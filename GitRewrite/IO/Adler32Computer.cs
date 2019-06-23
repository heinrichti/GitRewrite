namespace GitRewrite.IO
{
    public class Adler32Computer
    {
        public static uint Checksum(byte[] data, int length)
        {
            uint adler = 1;

            uint s1 = adler & 0xffff;
            uint s2 = adler >> 16;

            for (int i = 0; i < length; i++)
            {
                s1 += data[i];
                s2 += s1;
                s1 %= 65521U;
                s2 %= 65521U;
            }

            return s1 | (s2 << 16);
        }
    }
}