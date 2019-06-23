namespace GitRewrite.IO
{
    public class Adler32Computer
    {
        private const int Modulus = 65521;

        public static int Checksum(byte[] data, int offset, int length)
        {
            int a = 1;
            int b = 0;

            for (int counter = 0; counter < length; ++counter)
            {
                a = (a + (data[offset + counter])) % Modulus;
                b = (b + a) % Modulus;
            }

            return ((b * 65536) + a);
        }
    }
}