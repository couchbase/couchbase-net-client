using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class ObserveTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new Observe(key, null, null, 0), "Key cannot be empty.");
        }

        [Test]
        public void Can_Write_Payload_When_Key_Has_Non_Standard_Characters()
        {
            var mockVBucket = new Mock<IVBucket>();
            mockVBucket.Setup(x => x.Index).Returns(0);

            const string key = "ääåaasdaéö";
            var operation = new Observe(key, mockVBucket.Object, new DefaultTranscoder(), 0);

            var result = operation.Write();
            Assert.IsNotNull(result);
        }
    }
}
