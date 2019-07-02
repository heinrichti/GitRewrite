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

        public readonly ObjectHash Hash;

        public readonly GitObjectType Type;

        public bool Equals(GitObjectBase other)
        {
            if (ReferenceEquals(null, other)) return false;

            if (ReferenceEquals(this, other)) return true;

            return Hash.Equals(other.Hash);
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