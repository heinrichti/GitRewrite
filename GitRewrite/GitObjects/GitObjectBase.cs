using System;

namespace GitRewrite.GitObjects
{
    public abstract class GitObjectBase : IEquatable<GitObjectBase>
    {
        private readonly int _hashCode;

        protected GitObjectBase(ObjectHash hash, GitObjectType type)
        {
            Hash = hash;
            Type = type;

            _hashCode = hash.GetHashCode();
        }

        public ObjectHash Hash { get; }

        public GitObjectType Type { get; }

        public bool Equals(GitObjectBase other)
        {
            if (ReferenceEquals(null, other)) return false;

            if (ReferenceEquals(this, other)) return true;

            return Hash.Equals(other.Hash);
        }

        protected static int IndexOf(in ReadOnlySpan<byte> byteSpan, char c)
        {
            for (var i = 0; i < byteSpan.Length; i++)
                if (byteSpan[i] == c)
                    return i;

            return -1;
        }

        protected static bool StartsWith(in ReadOnlyMemory<byte> memory, string str)
        {
            if (str.Length > memory.Length)
                return false;

            for (var i = 0; i < str.Length; i++)
                if (memory.Span[i] != str[i])
                    return false;

            return true;
        }

        public abstract byte[] SerializeToBytes();

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;

            if (ReferenceEquals(this, obj)) return true;

            if (obj.GetType() != GetType()) return false;

            return Equals((GitObjectBase) obj);
        }

        public override int GetHashCode() => _hashCode;
    }
}