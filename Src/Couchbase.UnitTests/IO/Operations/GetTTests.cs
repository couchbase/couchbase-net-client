using System.Reflection;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class GetTTests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Test_Empty_Key_Throws_KeyException(string key)
        {
            Assert.Throws<MissingKeyException>(() => new GetT<dynamic>(key, null, null, 0), "Key cannot be empty.");
        }

        [Test]
        public void When_ErrorCode_Is_Null_CanRetry_Returns_False()
        {
            var get = new GetT<dynamic>("thekey", null, new DefaultTranscoder(), 0);
            Assert.False(get.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Null_And_Has_Cas_CanRetry_Returns_True()
        {
            var get = new GetT<dynamic>("thekey", null, new DefaultTranscoder(), 0)
            {
                Cas = 1010100010
            };
            Assert.True(get.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_None_CanRetry_Returns_False()
        {
            var get = new GetT<dynamic>("thekey", null, new DefaultTranscoder(), 0);
            get.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(get, new ErrorCode());
            Assert.False(get.CanRetry());
        }

        [Test]
        [TestCase(RetryStrategy.Linear)]
        [TestCase(RetryStrategy.Constant)]
        [TestCase(RetryStrategy.Exponential)]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_Not_None_CanRetry_Returns_True(RetryStrategy strategy)
        {
            var get = new GetT<dynamic>("thekey", null, new DefaultTranscoder(), 0);
            get.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(get, new ErrorCode
            {
                Retry = new RetrySpec
                {
                    Strategy = strategy
                }
            });
            Assert.True(get.CanRetry());
        }
    }
}
