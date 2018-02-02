using System.Reflection;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class AddTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new Add<dynamic>(key, new { foo = "foo" }, null, null, 0), "Key cannot be empty.");
        }

        [Test]
        public void When_Cloned_Expires_Is_Copied()
        {
            var set = new Add<string>("key", "value", null, new DefaultTranscoder(), 1000)
            {
                Expires = 10
            };

            Assert.AreEqual(10, set.Expires);
            var cloned = set.Clone() as Add<string>;
            Assert.AreEqual(10, cloned.Expires);
        }

        [Test]
        public void When_ErrorCode_Is_Null_CanRetry_Returns_True()
        {
            var add = new Add<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            Assert.True(add.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_None_CanRetry_Returns_False()
        {
            var add = new Add<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            add.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(add, new ErrorCode());
            Assert.False(add.CanRetry());
        }

        [Test]
        [TestCase(RetryStrategy.Linear)]
        [TestCase(RetryStrategy.Constant)]
        [TestCase(RetryStrategy.Exponential)]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_Not_None_CanRetry_Returns_True(RetryStrategy strategy)
        {
            var add = new Add<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            add.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(add, new ErrorCode
            {
                Retry = new RetrySpec
                {
                    Strategy = strategy
                }
            });
            Assert.True(add.CanRetry());
        }
    }
}
