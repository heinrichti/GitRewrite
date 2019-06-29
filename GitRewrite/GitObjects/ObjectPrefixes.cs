using System.Linq;

namespace GitRewrite.GitObjects
{
    public static class ObjectPrefixes
    {
        public static readonly byte[] TreePrefix = "tree ".Select(x => (byte) x).ToArray();
        public static readonly byte[] ParentPrefix = "parent ".Select(x => (byte) x).ToArray();
        public static readonly byte[] AuthorPrefix = "author ".Select(x => (byte) x).ToArray();
        public static readonly byte[] CommitterPrefix = "committer ".Select(x => (byte) x).ToArray();
        public static readonly byte[] GpgSigPrefix = "gpgsig ".Select(x => (byte) x).ToArray();
        public static readonly byte[] TagPrefix = "tag ".Select(x => (byte) x).ToArray();
        public static readonly byte[] TaggerPrefix = "tagger ".Select(x => (byte) x).ToArray();
        public static readonly byte[] ObjectPrefix = "object ".Select(x => (byte) x).ToArray();
        public static readonly byte[] TypePrefix = "type ".Select(x => (byte) x).ToArray();
    }
}