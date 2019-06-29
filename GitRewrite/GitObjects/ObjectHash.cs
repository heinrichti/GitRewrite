using System;
using System.Linq;

namespace GitRewrite.GitObjects
{
    public struct ObjectHash : IEquatable<ObjectHash>
    {
        private readonly int _hashCode;

        public ObjectHash(byte[] hash)
        {
            if (hash.Length != 20)
                throw new ArgumentException();

            Bytes = hash;

            unchecked
            {
                _hashCode = 0;
                for (var index = hash.Length - 1; index >= 0; index--)
                {
                    var b = hash[index];
                    _hashCode = (_hashCode * 31) ^ b;
                }
            }
        }

        public ObjectHash(string hash) : this(Hash.StringToByteArray(hash))
        {
        }

        public ObjectHash(in ReadOnlySpan<byte> hashStringAsBytes)
            : this(Hash.HashStringToByteArray(hashStringAsBytes))
        {

        }

        public byte[] Bytes { get; }

        public override string ToString() => Hash.ByteArrayToHexViaLookup32(Bytes);

        public bool Equals(ObjectHash other) => Bytes.AsSpan().SpanEquals(other.Bytes.AsSpan());

        public override bool Equals(object obj) => obj is ObjectHash other && Equals(other);

        public override int GetHashCode()
            => _hashCode;

        public static bool operator !=(ObjectHash h1, ObjectHash h2) => !h1.Equals(h2);

        public static bool operator ==(ObjectHash h1, ObjectHash h2) => h1.Equals(h2);
    }
}