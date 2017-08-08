using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class SetTests
    {
        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var set = new Set<string>("key", "value", null, new DefaultTranscoder(), 1000)
            {
                Expires = 10
            };

            Assert.AreEqual(10, set.Expires);
            var cloned = set.Clone() as Set<string>;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
