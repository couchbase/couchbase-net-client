using System.Reflection;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using NUnit.Framework;

namespace Couchbase.UnitTests.IO.Operations
{
    [TestFixture]
    public class GetLTests
    {
        [Test]
        public void Sets_Expiration_Value_In_Extras()
        {
            const uint expiration = 10;
            var transcoder = new DefaultTranscoder();
            var operation = new GetL<dynamic>("key", null, transcoder, 0)
            {
                Expiration = expiration
            };

            var bytes = operation.Write();
            Assert.AreEqual(expiration, transcoder.Converter.ToUInt32(bytes, 24));
        }

        [Test]
        public void When_ErrorCode_Is_Null_CanRetry_Returns_True()
        {
            var get = new GetL<dynamic>("thekey", null, new DefaultTranscoder(), 0);
            Assert.True(get.CanRetry());
        }

        [Test]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_None_CanRetry_Returns_False()
        {
            var get = new GetL<dynamic>("thekey", null, new DefaultTranscoder(), 0);
            get.GetType().GetField("ErrorCode", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(get, new ErrorCode());
            Assert.False(get.CanRetry());
        }

        [Test]
        [TestCase(RetryStrategy.Linear)]
        [TestCase(RetryStrategy.Constant)]
        [TestCase(RetryStrategy.Exponential)]
        public void When_ErrorCode_Is_Not_Null_And_RetryStrategy_Is_Not_None_CanRetry_Returns_True(RetryStrategy strategy)
        {
            var get = new GetL<dynamic>("thekey", null, new DefaultTranscoder(), 0);
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
