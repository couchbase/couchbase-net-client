using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.DecrementTests
{
    [TestFixture]
    public class CloneTests
    {
        [Test]
        public void Clone_SetsProperties()
        {
            // Arrange
            var bucket = Mock.Of<IVBucket>();
            var transcoder = new DefaultTranscoder();

            var operation = new Decrement("key", 1, 1, bucket, transcoder, 500)
            {
                Attempts = 1,
                Cas = 1123,
                BucketName = "bucket",
                LastConfigRevisionTried = 2,
                ErrorCode = new ErrorCode(),
                Expires = 3
            };

            // Act

            var cloned = operation.Clone();

            // Assert

            Assert.AreSame(operation.VBucket, cloned.VBucket);
            Assert.AreEqual(operation.Key, cloned.Key);
            Assert.AreEqual(operation.Opaque, cloned.Opaque);

            Assert.AreEqual(operation.Attempts, cloned.Attempts);
            Assert.AreEqual(operation.Cas, cloned.Cas);
            Assert.AreEqual(operation.CreationTime, cloned.CreationTime);
            Assert.AreEqual(operation.LastConfigRevisionTried, cloned.LastConfigRevisionTried);
            Assert.AreEqual(operation.BucketName, cloned.BucketName);

            var clonedDecrement = cloned as Decrement;
            Assert.NotNull(clonedDecrement);
            Assert.AreEqual(operation.MutationToken, clonedDecrement.MutationToken);
            Assert.AreSame(operation.ErrorCode, clonedDecrement.ErrorCode);
            Assert.AreEqual(operation.Expires, clonedDecrement.Expires);
        }
    }
}
