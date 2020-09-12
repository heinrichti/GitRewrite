using System;

namespace GitRewrite.GitObjects
{
    public readonly struct ObjectHash : IEquatable<ObjectHash>
    {
        private readonly int _hashCode;
        private const int ByteHashLength = 20;

        public static ObjectHash Empty;

        public ObjectHash(byte[] hash)
        {
            if (hash.Length != ByteHashLength)
                throw new ArgumentException();

            Bytes = hash;

            unchecked
            {
                _hashCode = 0;
                for (var index = 0; index < ByteHashLength; index++)
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

        public readonly byte[] Bytes;

        public override string ToString() => Hash.ByteArrayToString(Bytes);

        public byte[] ToStringBytes() => Hash.ByteArrayToTextBytes(Bytes);

        public bool Equals(ObjectHash other)
        {
            if ((_hashCode != other._hashCode) || (Bytes == null && other.Bytes != null) || (Bytes != null && other.Bytes == null))
                return false;

            if (Bytes == null && other.Bytes == null)
                return true;

            for (int i = 0; i < 20; i++)
            {
                if (Bytes[i] != other.Bytes[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj) => obj is ObjectHash other && Equals(other);

        public override int GetHashCode()
            => _hashCode;

        public static bool operator !=(ObjectHash h1, ObjectHash h2) => !h1.Equals(h2);

        public static bool operator ==(ObjectHash h1, ObjectHash h2) => h1.Equals(h2);
    }
}