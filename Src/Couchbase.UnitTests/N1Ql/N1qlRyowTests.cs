using System;
using Couchbase.Core.Buckets;
using Couchbase.N1QL;
using Moq;
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
            document1.Setup(x => x.Token).Returns(new MutationToken(102, 22, 8282) {BucketRef = "bucket1_name" });

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken(123, 11, 8332) { BucketRef = "bucket1_name" });

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken(133, 23, 333) { BucketRef = "bucket2_name" });

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
                ScanConsistency(ScanConsistency.AtPlus);

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[8282,\"22\"],\"123\":[8332,\"11\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetFormValues_MultipleTokensSameVBucketId_HighestSequenceNumberIsUsed()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken(102, 22, 8282) { BucketRef = "bucket1_name" });

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken(102, 11, 8332) { BucketRef = "bucket1_name" });

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken(133, 23, 333) { BucketRef = "bucket2_name" });

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
                ScanConsistency(ScanConsistency.AtPlus);

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[8332,\"11\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void GetFormValues_MultipleTokensSameVBucketId_HighestSequenceNumberIsUsed2()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken(102, 22, 9999) { BucketRef = "bucket1_name" });

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken(102, 11, 8332) { BucketRef = "bucket1_name" });

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken(133, 23, 333) { BucketRef = "bucket2_name" });

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
                ScanConsistency(ScanConsistency.AtPlus);

            var actual = queryRequest.GetFormValues()["scan_vectors"];
            var expected = "{\"bucket1_name\":{\"102\":[9999,\"22\"]},\"bucket2_name\":{\"133\":[333,\"23\"]}}";

            Assert.AreEqual(expected, actual);
        }

        [Test]
        public void ScanConsistency_AtPlus_DoesNotThrow_NotSupportedException()
        {
            Assert.DoesNotThrow(()=>new QueryRequest("SELECT * FROM `bucket1_name`;").ScanConsistency(ScanConsistency.AtPlus));
        }

        [Test]
        public void MutationState_WhenDocumentDoesNotContainMutationToken_ThrowsArgumentException()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken(-1, -1, -1));
            Assert.Throws<ArgumentException>(() => new MutationState().Add(document1.Object));
        }

        [Test]
        public void MutationState_WhenDocumentFragementDoesNotContainMutationToken_ThrowsArgumentException()
        {
            var fragment = new Mock<IDocumentFragment<dynamic>>();
            fragment.Setup(x => x.Token).Returns(new MutationToken(-1, -1, -1));
            Assert.Throws<ArgumentException>(() => new MutationState().Add(fragment.Object));
        }

        [Test]
        public void MutationState_WhenScanConsistencyIsNotAtPlus_ThrowsArgumentException()
        {
            var document1 = new Mock<IDocument<dynamic>>();
            document1.Setup(x => x.Token).Returns(new MutationToken(102, 22, 8282) { BucketRef = "bucket1_name" });

            var document2 = new Mock<IDocument<dynamic>>();
            document2.Setup(x => x.Token).Returns(new MutationToken(123, 11, 8332) { BucketRef = "bucket1_name" });

            var document3 = new Mock<IDocument<dynamic>>();
            document3.Setup(x => x.Token).Returns(new MutationToken(133, 23, 333) { BucketRef = "bucket2_name" });

            var queryRequest = new QueryRequest("SELECT * FROM `bucket1_name`;").
                ConsistentWith(MutationState.From(document1.Object, document2.Object, document3.Object)).
                ScanConsistency(ScanConsistency.NotBounded);

            Assert.Throws<ArgumentException>(() => queryRequest.GetFormValues());
        }
    }
}
