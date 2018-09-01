using System;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.CouhbaseBucketTests
{
    [TestFixture]
    public class DecrementTests
    {
        private readonly IByteConverter _converter = new DefaultConverter();
        private readonly ITypeTranscoder _transcoder = new DefaultTranscoder();

        [Test]
        public void Decrement_With_Key_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key");
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(1, operation.Delta);
            Assert.AreEqual(1, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(0, operation.Expires);
            Assert.AreEqual(2, operation.Timeout);
        }

        [Test]
        public void Decrement_With_Timeout_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", TimeSpan.FromSeconds(10));
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(1, operation.Delta);
            Assert.AreEqual(1, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(0, operation.Expires);
            Assert.AreEqual(10, operation.Timeout);
        }

        [Test]
        public void Decrement_With_Delta_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2);
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(1, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(0, operation.Expires);
            Assert.AreEqual(2, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaTimeout_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, TimeSpan.FromSeconds(10));
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(1, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(0, operation.Expires);
            Assert.AreEqual(10, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaInitial_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, 4);
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(4, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(0, operation.Expires);
            Assert.AreEqual(2, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaInitialExpirationTime_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, 4, TimeSpan.FromSeconds(10));
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(4, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(10, operation.Expires);
            Assert.AreEqual(2, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaInitialExpiration_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, 4, 10);
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(4, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(10, operation.Expires);
            Assert.AreEqual(2, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaInitialExpirationTimeTimeout_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, 4, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20));
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(4, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(10, operation.Expires);
            Assert.AreEqual(20, operation.Timeout);
        }

        [Test]
        public void Decrement_With_DeltaInitialExpirationTimeout_ExecutesCorrectOperation()
        {
            // Arrange

            Decrement operation = null;

            var mockRequestExecuter = new Mock<IRequestExecuter>();
            mockRequestExecuter
                .Setup(m => m.SendWithRetry(It.IsAny<Decrement>()))
                .Callback((IOperation<ulong> op) => operation = (Decrement)op);

            // Act

            using (var bucket = new CouchbaseBucket(mockRequestExecuter.Object, _converter, _transcoder))
            {
                bucket.Name = "bucket";

                bucket.Decrement("key", 2, 4, 10, TimeSpan.FromSeconds(20));
            }

            // Assert

            Assert.NotNull(operation);
            Assert.AreEqual(2, operation.Delta);
            Assert.AreEqual(4, operation.Initial);
            Assert.AreEqual("bucket", operation.BucketName);
            Assert.AreEqual(10, operation.Expires);
            Assert.AreEqual(20, operation.Timeout);
        }
    }
}
