using System;
using System.Text;
using GitRewrite.GitObjects;
using GitRewrite.IO;

namespace GitRewrite
{
    public static class GitObjectFactory
    {
        public static Commit CommitFromContentBytes(byte[] contentBytes)
        {
            var hash = GetObjectHash(GitObjectType.Commit, contentBytes);
            return new Commit(hash, contentBytes);
        }

        public static Tag TagFromContentBytes(byte[] contentBytes)
        {
            var hash = GetObjectHash(GitObjectType.Tag, contentBytes);
            return new Tag(hash, contentBytes);
        }

        public static Tree TreeFromContentBytes(byte[] contentBytes)
        {
            var hash = GetObjectHash(GitObjectType.Tree, contentBytes);
            return new Tree(hash, contentBytes);
        }

        public static byte[] GetBytesWithHeader(GitObjectType type, byte[] contentBytes)
        {
            string header;
            if (type == GitObjectType.Commit)
                header = "commit " + contentBytes.Length + '\0';
            else if (type == GitObjectType.Tag)
                header = "tag " + contentBytes.Length + '\0';
            else if (type == GitObjectType.Tree)
                header = "tree " + contentBytes.Length + '\0';
            else if (type == GitObjectType.Blob)
                header = "blob " + contentBytes.Length + '\0';
            else
                throw new NotImplementedException();

            var headerBytes = Encoding.ASCII.GetBytes(header);
            var resultBuffer = new byte[headerBytes.Length + contentBytes.Length];
            Array.Copy(headerBytes, resultBuffer, headerBytes.Length);
            Array.Copy(contentBytes, 0, resultBuffer, headerBytes.Length, contentBytes.Length);

            return resultBuffer;
        }

        private static ObjectHash GetObjectHash(GitObjectType type, byte[] contentBytes)
        {
            var bytesWithHeader = GetBytesWithHeader(type, contentBytes);
            var hash = new ObjectHash(Hash.Create(bytesWithHeader));
            return hash;
        }

        public static GitObjectBase ReadGitObject(string repositoryPath, ObjectHash hash)
        {
            var gitObject = PackReader.GetObject(repositoryPath, hash);
            if (gitObject != null)
                return gitObject;

            var fileContent = HashContent.FromFile(repositoryPath, hash.ToString());
            var contentIndex = fileContent.AsSpan(7).IndexOf<byte>(0) + 8;

            if (IsCommit(fileContent))
                return new Commit(hash, fileContent.AsSpan(contentIndex).ToArray());

            if (IsTree(fileContent))
                return new Tree(hash, fileContent.AsMemory(contentIndex));

            if (IsTag(fileContent)) 
                return new Tag(hash, fileContent.AsMemory(contentIndex));

            // TODO blobs probably not working atm
            if (IsBlob(fileContent)) 
                return new Blob(hash, fileContent.AsMemory(contentIndex));

            return null;
        }

        public static Commit ReadCommit(string repositoryPath, ObjectHash hash)
        {
            var commit = PackReader.GetCommit(repositoryPath, hash);
            if (commit != null)
                return commit;

            var fileContent = HashContent.FromFile(repositoryPath, hash.ToString());

            if (IsCommit(fileContent))
                return new Commit(hash,
                    fileContent.AsMemory(fileContent.AsSpan(7).IndexOf<byte>(0) + 8).ToArray());

            throw new ArgumentException("Not a commit: " + hash);
        }

        public static Tree ReadTree(string repositoryPath, ObjectHash hash)
        {
            var tree = PackReader.GetTree(repositoryPath, hash);
            if (tree != null)
                return tree;

            var fileContent = HashContent.FromFile(repositoryPath, hash.ToString());

            if (IsTree(fileContent)) return new Tree(hash, 
                fileContent.AsMemory(fileContent.AsSpan(5).IndexOf<byte>(0) + 6));

            return null;
        }

        private static bool IsTag(in ReadOnlySpan<byte> fileContent) => AsciiBytesStartWith(fileContent, "tag ");

        private static bool IsTree(in ReadOnlySpan<byte> fileContent) => AsciiBytesStartWith(fileContent, "tree ");

        private static bool AsciiBytesStartWith(in ReadOnlySpan<byte> bytes, string str)
        {
            if (bytes.Length < str.Length)
                return false;

            for (var i = 0; i < str.Length; i++)
                if (bytes[i] != str[i])
                    return false;

            return true;
        }

        private static bool IsBlob(in ReadOnlySpan<byte> fileContent) => AsciiBytesStartWith(fileContent, "blob ");

        private static bool IsCommit(in ReadOnlySpan<byte> fileContent) => AsciiBytesStartWith(fileContent, "commit ");
    }
}