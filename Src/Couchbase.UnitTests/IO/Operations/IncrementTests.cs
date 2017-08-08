using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class IncrementTests
    {
        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var op = new Increment("key", 1, 1, null, new DefaultTranscoder(), 1000)
            {
                Expires = 10
            };

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as Increment;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
