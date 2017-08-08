using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class ReplaceTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new Replace<dynamic>(key, new { foo = "foo" }, null, null, 0), "Key cannot be empty.");
        }

        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var op = new Replace<string>("key", "value", null, new DefaultTranscoder(), 1000)
            {
                Expires = 10
            };

            Assert.AreEqual(10, op.Expires);
            var cloned = op.Clone() as Replace<string>;
            Assert.AreEqual(10, cloned.Expires);
        }
    }
}
