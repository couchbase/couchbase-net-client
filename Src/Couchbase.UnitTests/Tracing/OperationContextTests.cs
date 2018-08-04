using Couchbase.Tracing;
using Couchbase.Utils;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class OperationContextTests
    {
        [Test]
        public void OperationContext_ToString_Returns_Json()
        {
            var context = new OperationContext("my-service", "correlation-id")
            {
                BucketName = "bucket",
                LocalEndpoint = "local-1234",
                RemoteEndpoint = "remote-1234",
                TimeoutMicroseconds = 123456
            };

            var json = JsonConvert.SerializeObject(new
            {
                s = context.ServiceType,
                i = context.OperationId,
                b = context.BucketName,
                l = context.LocalEndpoint,
                r = context.RemoteEndpoint,
                t = context.TimeoutMicroseconds
            }, Formatting.None);

            var expected = string.Join(" ", ExceptionUtil.OperationTimeout, json).Replace("{","[").Replace("}", "]");
            Assert.AreEqual(expected, context.ToString());
        }
    }
}

