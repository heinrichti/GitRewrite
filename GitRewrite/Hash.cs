using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using GitRewrite.GitObjects;

namespace GitRewrite
{
    public class Hash
    {
        private static readonly uint[] Lookup32 = CreateLookup32();

        public static byte[] Create(byte[] data)
        {
            using (var sha1Hash = SHA1.Create())
            {
                var computedHash = sha1Hash.ComputeHash(data);
                return computedHash;
            }
        }

        private static uint[] CreateLookup32()
        {
            var result = new uint[256];
            for (var i = 0; i < 256; i++)
            {
                var s = i.ToString("x2");
                result[i] = s[0] + ((uint) s[1] << 16);
            }

            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte GetHashValue(char c)
        {
            if (c == '0') return 0;
            if (c == '1') return 1;
            if (c == '2') return 2;
            if (c == '3') return 3;
            if (c == '4') return 4;
            if (c == '5') return 5;
            if (c == '6') return 6;
            if (c == '7') return 7;
            if (c == '8') return 8;
            if (c == '9') return 9;
            if (c == 'a') return 10;
            if (c == 'A') return 10;
            if (c == 'b') return 11;
            if (c == 'B') return 11;
            if (c == 'c') return 12;
            if (c == 'C') return 12;
            if (c == 'd') return 13;
            if (c == 'D') return 13;
            if (c == 'e') return 14;
            if (c == 'E') return 14;
            if (c == 'f') return 15;
            if (c == 'F') return 15;

            throw new ArgumentException();
        }

        public static byte[] HashStringToByteArray(in ReadOnlySpan<byte> hashStringAsBytes)
        {
            var resultSize = hashStringAsBytes.Length / 2;
            var result = new byte[resultSize];

            for (int i = 0; i < resultSize; i++)
                result[i] = (byte) ((GetHashValue((char) hashStringAsBytes[2 * i]) << 4) |
                                    GetHashValue((char) hashStringAsBytes[2 * i + 1]));

            return result;
        }

        public static byte[] StringToByteArray(ReadOnlySpan<char> hash)
        {
            var resultSize = hash.Length / 2;
            var result = new byte[resultSize];

            for (int i = 0; i < resultSize; i++)
                result[i] = (byte) ((GetHashValue(hash[2 * i]) << 4) | GetHashValue(hash[2 * i + 1]));

            return result;
        }

        public static string ByteArrayToHexViaLookup32(byte[] bytes)
        {
            var lookup32 = Lookup32;

            return string.Create<(byte[] Bytes, uint[] Lookup)>(
                bytes.Length * 2, 
                (bytes, lookup32), (result, state) =>
                {
                    for (var i = 0; i < state.Bytes.Length; i++)
                    {
                        var val = state.Lookup[state.Bytes[i]];
                        result[2 * i] = (char) val;
                        result[2 * i + 1] = (char) (val >> 16);
                    }
                });
        }

        public static IEnumerable<ObjectHash> GetRewrittenParentHashes(IEnumerable<ObjectHash> hashes,
            Dictionary<ObjectHash, ObjectHash> rewrittenCommitHashes)
        {
            foreach (var parentHash in hashes)
            {
                var rewrittenParentHash = parentHash;

                while (rewrittenCommitHashes.TryGetValue(rewrittenParentHash, out var parentCommitHash))
                    rewrittenParentHash = parentCommitHash;

                yield return rewrittenParentHash;
            }
        }

        public static ObjectHash GetRewrittenParentHash(Commit commit,
            Dictionary<ObjectHash, ObjectHash> rewrittenCommitHashes)
        {
            var parentHash = commit.Parents.Single();

            while (rewrittenCommitHashes.TryGetValue(parentHash, out var parentCommitHash))
                parentHash = parentCommitHash;

            return parentHash;
        }
    }
}