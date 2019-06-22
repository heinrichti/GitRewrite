namespace GitRewrite
{
    public class Ref
    {
        public readonly string Hash;
        public readonly string Name;

        public Ref(string hash, string name)
        {
            Hash = hash;
            Name = name;
        }
    }

    public class TagRef : Ref
    {
        public readonly string CommitHash;

        public TagRef(string hash, string name, string commitHash)
            : base(hash, name)
        {
            CommitHash = commitHash;
        }
    }
}