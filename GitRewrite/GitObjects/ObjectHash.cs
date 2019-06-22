using System;

namespace GitRewrite.GitObjects
{
    public struct ObjectHash : IEquatable<ObjectHash>
    {
        public ObjectHash(byte[] hash) => Bytes = hash;

        public ObjectHash(string hash) : this(Hash.StringToByteArray(hash))
        {
        }

        public byte[] Bytes { get; }

        public override string ToString() => Hash.ByteArrayToHexViaLookup32(Bytes);

        public bool Equals(ObjectHash other)
        {
            if (Bytes.Length != other.Bytes.Length)
                return false;

            for (var i = 0; i < Bytes.Length; i++)
                if (Bytes[i] != other.Bytes[i])
                    return false;

            return true;
        }

        public override bool Equals(object obj) => obj is ObjectHash other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                for (var index = 0; index < Bytes.Length; index++)
                {
                    var b = Bytes[index];
                    result = (result * 31) ^ b;
                }

                return result;
            }
        }

        public static bool operator !=(ObjectHash h1, ObjectHash h2) => !h1.Equals(h2);

        public static bool operator ==(ObjectHash h1, ObjectHash h2) => h1.Equals(h2);
    }
}