using System.Linq;
using System.Text;
using GitRewrite.GitObjects;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRewrite.Tests
{
    [TestClass]
    public class CommitTest
    {
        private const string CommitWithPgpSig =
            "tree 413f6b2e45859c6a962d2f2e8437598c63d92e3a\n" +
            "parent d5191b5c947f79bb35960075621c273dc0bd0109\n" +
            "author Test User <2929650+test@users.noreply.github.com> 1561663031 +0200\n" +
            "committer GitHub <noreply@github.com> 1561663031 +0200\n" +
            "gpgsig -----BEGIN PGP SIGNATURE-----\n\n wsBcBAABCAAQBQJdFRY3CRBK7hj4Ov3rIwAAdHIIAFZzg7W6IVM3wyRvDXklc+yB\n kPFEkE89/G45rzvt+uY0T65itwjLlLVU9bvvSZsbzir9Fr3RlE3RaynDuebVyoBF\n m2ZYggN95R/MQvSvMnE64J5kguziGCnb+vWKFyfb51Iz4sw8JaX6hLQkmttPWAFQ\n 9cRaxyCJALGdKuIYS1POKa0LctU2lBHlUqO/Lqh5344gErenYHJFbBbd3M9OmFYL\n dxld9mPnC6K9NmdZHsBXDgQBqoJ7btKpqP2NUPvZEvYdLI0XkGS/OgN862QZFQoY\n dHqkLrNGhjFd1GPHJoVyAke0Q8z8V4P34uMCZpuAoqSkn2ODhhrNqfqGrjwUBaE=\n =MC9b\n -----END PGP SIGNATURE-----\n" +
            "\n\nUpdated build instructions";

        private const string CommitWithPgpSigHash = "699834ab5a722a0dea84743d7bf92e4e6082531f";

        private const string CommitWithoutPgpSig =
            "tree f7bbd02edd480914d28633f4404e2600d93af690\n" +
            "parent 6f6fa334d886eb104e46452d7e2aae5b9fbcc102\n" +
            "author Test User <test@gmail.com> 1561201571 +0200\ncommitter Test User <test@gmail.com> 1561201571 +0200\n\nGitignore";

        private const string CommitHash = "57516518659c81012449f16bacda5b36ddc25433";

        [TestMethod]
        public void CommitFromByte()
        {
            var commitBytes = Encoding.UTF8.GetBytes(CommitWithoutPgpSig);
            var commit = new Commit(new ObjectHash(CommitHash), commitBytes);

            Assert.AreEqual("f7bbd02edd480914d28633f4404e2600d93af690", commit.TreeHash.ToString());
            Assert.AreEqual("6f6fa334d886eb104e46452d7e2aae5b9fbcc102", commit.Parents.Single().ToString());
            Assert.AreEqual("Test User <test@gmail.com>", commit.GetAuthorName());
            Assert.AreEqual("\nGitignore", commit.CommitMessage);

            Assert.AreEqual(commitBytes, commit.SerializeToBytes());

            var sameCommitBytes =
                Commit.GetSerializedCommitWithChangedTreeAndParents(commit, commit.TreeHash, commit.Parents);
            CollectionAssert.AreEqual(commitBytes, sameCommitBytes);

            var newTreeHash = "1234567890123456789012345678901234567890";
            var newParent1 = "3216549870321654987032165498703216549870";
            var newParent2 = "9999999999999999999999999999999999999999";
            var newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit,
                new ObjectHash(newTreeHash),
                new[]
                {
                    new ObjectHash(newParent1),
                    new ObjectHash(newParent2)
                }.ToList());

            var newCommit = new Commit(new ObjectHash("1234567890123456789012345678901234567890"), newCommitBytes);

            Assert.AreEqual(newTreeHash, newCommit.TreeHash.ToString());
            Assert.AreEqual(2, newCommit.Parents.Count);
            Assert.AreEqual(newParent1, newCommit.Parents.First().ToString());
            Assert.AreEqual(newParent2, newCommit.Parents.Last().ToString());
            Assert.AreEqual("Test User <test@gmail.com>", newCommit.GetAuthorName());
            Assert.AreEqual("\nGitignore", newCommit.CommitMessage);
        }

        [TestMethod]
        public void CommitFromByteWithPgpSig()
        {
            var commitBytes = Encoding.UTF8.GetBytes(CommitWithPgpSig);
            var commit = new Commit(new ObjectHash(CommitWithPgpSigHash), commitBytes);

            Assert.AreEqual("413f6b2e45859c6a962d2f2e8437598c63d92e3a", commit.TreeHash.ToString());
            Assert.AreEqual("d5191b5c947f79bb35960075621c273dc0bd0109", commit.Parents.Single().ToString());
            Assert.AreEqual("Test User <2929650+test@users.noreply.github.com>", commit.GetAuthorName());
            Assert.AreEqual("\nUpdated build instructions", commit.CommitMessage);

            Assert.AreEqual(commitBytes, commit.SerializeToBytes());

            var newTreeHash = "1234567890123456789012345678901234567890";
            var newParent1 = "3216549870321654987032165498703216549870";
            var newParent2 = "9999999999999999999999999999999999999999";
            var newCommitBytes = Commit.GetSerializedCommitWithChangedTreeAndParents(commit,
                new ObjectHash(newTreeHash),
                new[]
                {
                    new ObjectHash(newParent1),
                    new ObjectHash(newParent2)
                }.ToList());

            var newCommit = new Commit(new ObjectHash("1234567890123456789012345678901234567890"), newCommitBytes);

            Assert.AreEqual(newTreeHash, newCommit.TreeHash.ToString());
            Assert.AreEqual(2, newCommit.Parents.Count);
            Assert.AreEqual(newParent1, newCommit.Parents.First().ToString());
            Assert.AreEqual(newParent2, newCommit.Parents.Last().ToString());
            Assert.AreEqual("Test User <2929650+test@users.noreply.github.com>", newCommit.GetAuthorName());
            Assert.AreEqual("\nUpdated build instructions", newCommit.CommitMessage);
        }
    }
}
