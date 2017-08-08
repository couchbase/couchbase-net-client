using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class TouchTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new Touch(key, null, null, 0), "Key cannot be empty.");
        }

        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var op = new Touch("key",  null, new DefaultTranscoder(), 1000)
            {
                Expires = 10
            };

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as Touch;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
