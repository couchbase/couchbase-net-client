using System.Reflection;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
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

        [Test]
        public void When_ErrorCode_Is_Null_CanRetry_Returns_False()
        {
            var set = new Set<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            Assert.False(set.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Null_And_Cas_Is_Set_CanRetry_Returns_True()
        {
            var set = new Set<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0)
            {
                Cas = 2
            };
            Assert.True(set.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_None_CanRetry_Returns_False()
        {
            var set = new Set<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            set.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(set, new ErrorCode());
            Assert.False(set.CanRetry());
        }

        [Test]
        [TestCase(RetryStrategy.Linear)]
        [TestCase(RetryStrategy.Constant)]
        [TestCase(RetryStrategy.Exponential)]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_Not_None_CanRetry_Returns_True(RetryStrategy strategy)
        {
            var set = new Set<dynamic>("thekey", "thevalue", null, new DefaultTranscoder(), 0);
            set.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(set, new ErrorCode
            {
                Retry = new RetrySpec
                {
                    Strategy = strategy
                }
            });
            Assert.True(set.CanRetry());
        }
    }
}
