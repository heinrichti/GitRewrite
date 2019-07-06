using System;
using System.Collections.Generic;
using System.Text;
using GitRewrite.GitObjects;
using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRewrite.Tests
{
    [TestClass]
    public class TagTests
    {
        private const string TagWithComment = "object 21b38c151f130550c42dbb6467855f20f36ca146\n" +
                                              "type commit\n" +
                                              "tag v1.2\n" +
                                              "tagger Test User <test.user@github.com> 1562258217 +0200\n\n" +
                                              "My Test Tag\n";

        private const string TagWithoutTagger =
            "object 1b2bf77246f78a91ffa90456e6d1c393db1d5eb0\ntype commit\ntag 1.1.95.0\n\nTagged Release xy";

        [TestMethod]
        public void TagWithoutTaggerTest()
        {
            var bytes = Encoding.UTF8.GetBytes(TagWithoutTagger);
            var objectHash = new ObjectHash("1234567890123456789012345678901234567890");
            var tag = new Tag(objectHash, bytes);

            Assert.AreEqual("1b2bf77246f78a91ffa90456e6d1c393db1d5eb0", tag.Object);
            Assert.AreEqual("commit", tag.TypeName);
            Assert.AreEqual("1.1.95.0", tag.TagName);
            Assert.AreEqual("\nTagged Release xy", tag.Message);
            Assert.AreEqual(string.Empty, tag.Tagger);
        }

        [TestMethod]
        public void TagWithCommentTest()
        {
            var bytes = Encoding.UTF8.GetBytes(TagWithComment);
            var objectHash = new ObjectHash("1234567890123456789012345678901234567890");
            var tag = new Tag(objectHash, bytes);

            Assert.AreEqual("v1.2", tag.TagName);
            Assert.AreEqual("\nMy Test Tag\n", tag.Message);
            Assert.IsFalse(tag.PointsToTag);
            Assert.IsFalse(tag.PointsToTree);
            Assert.AreEqual("Test User <test.user@github.com> 1562258217 +0200", tag.Tagger);
            Assert.AreEqual("commit", tag.TypeName);
            Assert.AreEqual("21b38c151f130550c42dbb6467855f20f36ca146", tag.Object);

            tag = new Tag( objectHash, tag.SerializeToBytes());
            Assert.AreEqual("v1.2", tag.TagName);
            Assert.AreEqual("\nMy Test Tag\n", tag.Message);
            Assert.IsFalse(tag.PointsToTag);
            Assert.IsFalse(tag.PointsToTree);
            Assert.AreEqual("Test User <test.user@github.com> 1562258217 +0200", tag.Tagger);
            Assert.AreEqual("commit", tag.TypeName);
            Assert.AreEqual("21b38c151f130550c42dbb6467855f20f36ca146", tag.Object);

            const string newObject = "3216549870321654987032165498703216549870";
            tag = tag.WithNewObject(newObject);
            Assert.AreEqual("v1.2", tag.TagName);
            Assert.AreEqual("\nMy Test Tag\n", tag.Message);
            Assert.IsFalse(tag.PointsToTag);
            Assert.IsFalse(tag.PointsToTree);
            Assert.AreEqual("Test User <test.user@github.com> 1562258217 +0200", tag.Tagger);
            Assert.AreEqual("commit", tag.TypeName);
            Assert.AreEqual(newObject, tag.Object);
        }
    }
}
