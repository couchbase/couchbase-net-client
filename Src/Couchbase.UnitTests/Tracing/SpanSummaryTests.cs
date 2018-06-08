using Couchbase.Tracing;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class SpanSummaryTests
    {
        [Test]
        public void Summary_ToString_Returns_Json()
        {
            var summary = new SpanSummary
            {
                OperationName = "get",
                ServiceType = "kv",
                TotalDuration = 100,
                ServerDuration = 12,
                EncodingDuration = 15,
                DecodingDuration = 34,
                LastDispatchDuration = 10,
                LastLocalId = "local-id",
                LastLocalAddress = "local-address",
                LastRemoteAddress = "remote-address",
                DispatchDuration = 8
            };

            var expected = JsonConvert.SerializeObject(new
            {
                operation_name =summary.OperationName,
                last_local_address = summary.LastLocalAddress,
                last_remote_address = summary.LastRemoteAddress,
                last_local_id = summary.LastLocalId,
                last_dispatch_us = summary.LastDispatchDuration,
                total_us = summary.TotalDuration,
                encode_us = summary.EncodingDuration,
                dispatch_us = summary.DispatchDuration,
                server_us = summary.ServerDuration,
                decode_us = summary.DecodingDuration,
            }, Formatting.None);

            var json = summary.ToString();
            Assert.AreEqual(expected, json);
        }
    }
}
