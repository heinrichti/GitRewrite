using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GitRewrite.IO
{
    public static class IdxOffsetReader
    {
        private const int HeaderLength = 8;
        private const int HashLength = 20;
        private const int FanoutLength = 4;
        private const int HashesTableStart = HeaderLength + 256 * FanoutLength;

        private static void VerifyHeader(byte[] header)
        {
            if (!IsPack(header)) throw new Exception("Not a idx file");
        }

        private static bool IsPack(byte[] header) =>
            header[0] == 255 && header[1] == 't' && header[2] == 'O' && header[3] == 'c' &&
            header[4] == 0 && header[5] == 0 && header[6] == 0 && header[7] == 2;

        private static int GetFileCountFromFanout(in ReadOnlySpan<byte> bytes)
        {
            var result = bytes[3] << 0;
            result += bytes[2] << 8;
            result += bytes[1] << 16;
            result += bytes[0] << 24;

            return result;
        }

        public static IEnumerable<(byte[] Hash, long Offset)> GetPackOffsets(string idxFile)
        {
            using (var fileStream = new FileStream(idxFile, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[HashesTableStart];

                fileStream.Read(buffer, 0, HashesTableStart);
                VerifyHeader(buffer);

                var objectCount = GetFileCountFromFanout(buffer.AsSpan(HeaderLength + 255 * FanoutLength));
                if (objectCount == 0)
                    yield break;

                var hashes = new Queue<byte[]>();
                using (var bufferedStream = new BufferedStream(fileStream, 4096))
                {
                    for (var i = 0; i < objectCount; i++)
                    {
                        var hash = new byte[20];
                        bufferedStream.Read(hash);
                        hashes.Enqueue(hash);
                    }

                    bufferedStream.Seek(HashesTableStart + HashLength * objectCount + 4 * objectCount,
                        SeekOrigin.Begin);

                    List<(byte[] Hash, long Offset)> largeOffsets = new List<(byte[], long)>();

                    for (var i = 0; i < objectCount; i++)
                    {
                        bufferedStream.Read(buffer, 0, 4);
                        var packOffset = buffer.AsSpan(0, 4);

                        long offset = packOffset[3];
                        offset += packOffset[2] << 8;
                        offset += packOffset[1] << 16;
                        offset += (packOffset[0] & 0b01111111) << 24;

                        if (MsbSet(packOffset))
                            largeOffsets.Add((hashes.Dequeue(), offset));
                        else
                            yield return (hashes.Dequeue(), offset);
                    }

                    bufferedStream.Seek(
                        HashesTableStart + HashLength * objectCount + 4 * objectCount +
                        4 * objectCount,
                        SeekOrigin.Begin);

                    foreach (var largeOffset in largeOffsets.Select(x => x.Hash))
                    {
                        bufferedStream.Read(buffer, 0, 8);
                        var packOffset = buffer.AsSpan(0, 8);
                        if (BitConverter.IsLittleEndian)
                            packOffset.Reverse();

                        yield return (largeOffset, BitConverter.ToInt64(packOffset));
                    }
                }
            }
        }

        public static bool MsbSet(in Span<byte> packOffset) => (packOffset[0] & 0b10000000) != 0;
    }
}