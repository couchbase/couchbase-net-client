using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations.DecrementTests
{
    [TestFixture]
    public class ConstructorTests
    {
        [Test]
        public void Constructor_SetsProperties()
        {
            // Arrange

            var key = "key";
            var initial = 1UL;
            var delta = 2UL;
            var transcoder = new DefaultTranscoder();
            var timeout = 1000U;

            // Act

            var operation = new Decrement(key, initial, delta, null, transcoder, timeout);

            // Assert

            Assert.AreEqual(key, operation.Key);
            Assert.AreEqual(initial, operation.Initial);
            Assert.AreEqual(delta, operation.Delta);
            Assert.AreEqual(transcoder, operation.Transcoder);
            Assert.AreEqual(timeout, operation.Timeout);
            Assert.AreEqual(OperationCode.Decrement, operation.OperationCode);
        }
    }
}
