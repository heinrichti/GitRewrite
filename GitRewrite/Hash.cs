using System;
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
        private static byte GetHashValue(byte c)
        {
            if (c <= 57)
                return (byte) (c - 48);

            return (byte) (c - 87);
        }

        public static byte[] HashStringToByteArray(in ReadOnlySpan<byte> hashStringAsBytes)
        {
            var resultSize = hashStringAsBytes.Length / 2;
            var result = new byte[resultSize];

            for (int i = 0; i < resultSize; i++)
                result[i] = (byte) ((GetHashValue(hashStringAsBytes[2 * i]) << 4) |
                                    GetHashValue(hashStringAsBytes[2 * i + 1]));

            return result;
        }

        public static byte[] StringToByteArray(ReadOnlySpan<char> hash)
        {
            var resultSize = hash.Length / 2;
            var result = new byte[resultSize];

            for (int i = 0; i < resultSize; i++)
                result[i] = (byte) ((GetHashValue((byte)hash[2 * i]) << 4) | GetHashValue((byte)hash[2 * i + 1]));

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