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
            byte result = 0;

            switch (c)
            {
                case '0':
                    result = 0;
                    break;
                case '1':
                    result = 1;
                    break;
                case '2':
                    result = 2;
                    break;
                case '3':
                    result = 3;
                    break;
                case '4':
                    result = 4;
                    break;
                case '5':
                    result = 5;
                    break;
                case '6':
                    result = 6;
                    break;
                case '7':
                    result = 7;
                    break;
                case '8':
                    result = 8;
                    break;
                case '9':
                    result = 9;
                    break;
                case 'a':
                case 'A':
                    result = 10;
                    break;
                case 'b':
                case 'B':
                    result = 11;
                    break;
                case 'c':
                case 'C':
                    result = 12;
                    break;
                case 'd':
                case 'D':
                    result = 13;
                    break;
                case 'e':
                case 'E':
                    result = 14;
                    break;
                case 'f':
                case 'F':
                    result = 15;
                    break;
                default:
                    throw new ArgumentException();
            }

            return result;
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

        public static byte[] ByteArrayToTextBytes(byte[] bytes)
        {
            var lookup32 = Lookup32;
            var result = new byte[bytes.Length * 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                var val = lookup32[bytes[i]];
                result[2 * i] = (byte) val;
                result[2 * i + 1] = (byte) (val >> 16);
            }

            return result;
        }

        public static string ByteArrayToString(byte[] bytes)
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