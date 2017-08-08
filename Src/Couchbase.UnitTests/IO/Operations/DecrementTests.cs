using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class DecrementTests
    {
        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var op = new Decrement("key", 1, 1, 10, null, new DefaultTranscoder(), 1000);

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as Decrement;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
