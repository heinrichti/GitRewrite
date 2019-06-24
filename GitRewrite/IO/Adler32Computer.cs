namespace GitRewrite.IO
{
    public class Adler32Computer
    {
        private const uint Base = 65521U;
        private const int Nmax = 5552;

        public static unsafe uint Checksum(byte[] buffer)
        {
            uint adler = 1;
            int len = buffer.Length;
            fixed (byte* fixedBufPointer = buffer)
            {
                var buf = fixedBufPointer;
                uint sum2 = (adler >> 16) & 0xffff;
                adler &= 0xffff;

                /* in case user likes doing a byte at a time, keep it fast */
                if (len == 1)
                {
                    adler += buf[0];
                    if (adler >= Base)
                        adler -= Base;
                    sum2 += adler;
                    if (sum2 >= Base)
                        sum2 -= Base;
                    return adler | (sum2 << 16);
                }

                /* initial Adler-32 value (deferred check for len == 1 speed) */
                if (len == 0)
                    return 1;

                /* in case short lengths are provided, keep it somewhat fast */
                if (len < 16)
                {
                    while (len-- != 0)
                    {
                        adler += (*buf)++; //[bufIndex++];
                        sum2 += adler;
                    }

                    if (adler >= Base)
                        adler -= Base;

                    sum2 %= Base;
                    return adler | (sum2 << 16);
                }

                /* do length NMAX blocks -- requires just one modulo operation */
                while (len >= Nmax)
                {
                    len -= Nmax;
                    var n = Nmax / 16;
                    do
                    {
                        adler += buf[0];
                        sum2 += adler;
                        adler += buf[1];
                        sum2 += adler;
                        adler += buf[2];
                        sum2 += adler;
                        adler += buf[3];
                        sum2 += adler;
                        adler += buf[4];
                        sum2 += adler;
                        adler += buf[5];
                        sum2 += adler;
                        adler += buf[6];
                        sum2 += adler;
                        adler += buf[7];
                        sum2 += adler;
                        adler += buf[8];
                        sum2 += adler;
                        adler += buf[9];
                        sum2 += adler;
                        adler += buf[10];
                        sum2 += adler;
                        adler += buf[11];
                        sum2 += adler;
                        adler += buf[12];
                        sum2 += adler;
                        adler += buf[13];
                        sum2 += adler;
                        adler += buf[14];
                        sum2 += adler;
                        adler += buf[15];
                        sum2 += adler;

                        buf += 16;
                    } while (--n != 0);

                    adler %= Base;
                    sum2 %= Base;
                }

                /* do remaining bytes (less than NMAX, still just one modulo) */
                if (len != 0)
                {
                    while (len >= 16)
                    {
                        len -= 16;

                        adler += buf[0];
                        sum2 += adler;
                        adler += buf[1];
                        sum2 += adler;
                        adler += buf[2];
                        sum2 += adler;
                        adler += buf[3];
                        sum2 += adler;
                        adler += buf[4];
                        sum2 += adler;
                        adler += buf[5];
                        sum2 += adler;
                        adler += buf[6];
                        sum2 += adler;
                        adler += buf[7];
                        sum2 += adler;
                        adler += buf[8];
                        sum2 += adler;
                        adler += buf[9];
                        sum2 += adler;
                        adler += buf[10];
                        sum2 += adler;
                        adler += buf[11];
                        sum2 += adler;
                        adler += buf[12];
                        sum2 += adler;
                        adler += buf[13];
                        sum2 += adler;
                        adler += buf[14];
                        sum2 += adler;
                        adler += buf[15];
                        sum2 += adler;

                        buf += 16;
                    }

                    while (len-- != 0)
                    {
                        adler += *buf++;
                        sum2 += adler;
                    }

                    adler %= Base;
                    sum2 %= Base;
                }

                /* return recombined sums */
                return adler | (sum2 << 16);
            }
        }
    }
}