using System;
using Couchbase.Core.Buckets;
using Couchbase.N1QL;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.N1Ql
{
    // ReSharper disable once InconsistentNaming
    [TestFixture]
    public class N1qlRyowTests
    {
        [Test]
        public void GetFormValues_WhenScanConsistenyIsAtPlus_ScanVectorsIsAddedToFormValues()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 22, 8282));

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 123, 11, 8332));

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken("bucket2_name", 133, 23, 333));

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
#pragma warning disable 618
                ScanConsistency(ScanConsistency.AtPlus);
#pragma warning restore 618

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[8282,\"22\"],\"123\":[8332,\"11\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, JsonConvert.SerializeObject(actual));
        }

        [Test]
        public void GetFormValues_MultipleTokensSameVBucketId_HighestSequenceNumberIsUsed()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 22, 8282));

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 11, 8332));

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken("bucket2_name", 133, 23, 333));

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
#pragma warning disable 618
                ScanConsistency(ScanConsistency.AtPlus);
#pragma warning restore 618

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[8332,\"11\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, JsonConvert.SerializeObject(actual));
        }

        [Test]
        public void GetFormValues_MultipleTokensSameVBucketId_HighestSequenceNumberIsUsed2()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 22, 9999));

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 11, 8332));

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken("bucket2_name", 133, 23, 333));

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
#pragma warning disable 618
                ScanConsistency(ScanConsistency.AtPlus);
#pragma warning restore 618

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[9999,\"22\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, JsonConvert.SerializeObject(actual));
        }

        [Test]
        public void ScanConsistency_AtPlus_DoesNotThrow_NotSupportedException()
        {
#pragma warning disable 618
            Assert.DoesNotThrow(()=>new QueryRequest("SELECT * FROM `bucket1_name`;").ScanConsistency(ScanConsistency.AtPlus));
#pragma warning restore 618
        }

        [Test]
        public void MutationState_WhenDocumentDoesNotContainMutationToken_ThrowsArgumentException()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken(null, -1, -1, -1));
            Assert.Throws<ArgumentException>(() => new MutationState().Add(document1.Object));
        }

        [Test]
        public void MutationState_WhenDocumentFragementDoesNotContainMutationToken_ThrowsArgumentException()
        {
            var fragment = new Mock<IDocumentFragment<dynamic>>();
            fragment.Setup(x => x.Token).Returns(new MutationToken(null, -1, -1, -1));
            Assert.Throws<ArgumentException>(() => new MutationState().Add(fragment.Object));
        }

        [Test]
        public void MutationState_WhenScanConsistencyIsNotAtPlus_ThrowsArgumentException()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 102, 22, 8282));

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken("bucket1_name", 123, 11, 8332));

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken("bucket2_name", 133, 23, 333));

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
                ScanConsistency(ScanConsistency.NotBounded);

            Assert.Throws<ArgumentException>(() => queryRequest.GetFormValues());
        }
    }
}
