using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations.SubDocument;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests
{
    [TestFixture]
    public class SubDocumentAPITests
    {
        [Test]
        public void Test()
        {
            var subDocResultMocked = new Mock<IDocumentFragment<object>>();
            subDocResultMocked.Setup(x => x.Content<string>(It.IsAny<string>())).Returns("foo");
            subDocResultMocked.Setup(x => x.Content<string>(It.IsAny<int>())).Returns("bar");
            subDocResultMocked.Setup(x => x.Exists(It.IsAny<string>())).Returns(true);

            var lookupInMocked = new Mock<ILookupInBuilder<object>>();
            lookupInMocked.Setup(x => x.Get(It.IsAny<string>())).Returns(lookupInMocked.Object);
            lookupInMocked.Setup(x => x.Execute()).Returns(subDocResultMocked.Object);

            var bucketMocked = new Mock<IBucket>();
            bucketMocked.Setup(x => x.LookupIn<object>(It.IsAny<string>())).Returns(lookupInMocked.Object);

            var bucket = bucketMocked.Object;
            var subDocResult = bucket.LookupIn<object>("thekey").
                Get("some.json.path").
                Execute();

            var content = subDocResult.Content<string>("somepath");
            var item = subDocResult.Content<string>(1);
            var exists = subDocResult.Exists("somepathtosomewhere");

            Assert.AreEqual("foo", content);
            Assert.AreEqual("bar", item);
            Assert.IsTrue(exists);
        }

        [Test]
        public void Get_AnyKey_WithPath_ExpectedResult()
        {
            var mockSubdocResult = new Mock<IDocumentFragment<object>>();
            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter.Setup(x => x.SendWithRetry(It.IsAny<SubGet<object>>())).Returns(mockSubdocResult.Object);
        }

        [Test]
        public void SubDocDelete_WillRetry_IfHasCas()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Remove("somepath");
            var op = new SubDocDelete<dynamic>(builder, "thekey", new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10) {Cas = 100};

            Assert.IsTrue(op.CanRetry());
        }

        [Test]
        public void SubDocDelete_WillNotRetry_IfCasIsZero()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Remove("somepath");
            var op = new SubDocDelete<dynamic>(builder, "thekey", new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 0 };

            Assert.IsFalse(op.CanRetry());
        }

        [Test]
        public void MultiMutation_WillRetry_IfHasCas()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Remove("somepath");
            var op = new MultiMutation<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10){Cas = 100};

            Assert.IsTrue(op.CanRetry());
        }

        [Test]
        public void MultiMutation_WillNotRetry_IfCasIsZero()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Remove("somepath");
            var op = new MultiMutation<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 0 };

            Assert.IsFalse(op.CanRetry());
        }

        [Test]
        public void MultiLookup_WillRetry_IfHasCas()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Get("pathone");
            builder.Get("pathtwo");
            var op = new MultiLookup<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 100 };

            Assert.IsTrue(op.CanRetry());
        }

        [Test]
        public void MultiLookup_WillRetry_IfCasIsZero()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Get("pathone");
            builder.Get("pathtwo");
            var op = new MultiLookup<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 0 };

            Assert.IsTrue(op.CanRetry());
        }

        [Test]
        public void MultiLookup_Clone()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new LookupInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Get("pathone");
            builder.Get("pathtwo");
            var op = new MultiLookup<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 100 };

            var cloned = (MultiLookup<dynamic>)op.Clone();
            Assert.AreEqual(op, cloned);
        }

        [Test]
        public void MultiMutate_Clone()
        {
            var mockedInvoker = new Mock<ISubdocInvoker>();
            var builder = new MutateInBuilder<dynamic>(mockedInvoker.Object, () => new DefaultSerializer(), "thekey");

            builder.Remove("somepath");
            builder.ArrayPrepend("pathone", 10);
            var op = new MultiMutation<dynamic>("thekey", builder, new Mock<IVBucket>().Object,
                new Mock<ITypeTranscoder>().Object, 10)
            { Cas = 100 };

            var cloned = (MultiMutation<dynamic>) op.Clone();
            Assert.AreEqual(op, cloned);
        }
    }
}
