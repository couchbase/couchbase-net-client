using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Tracing;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Tracing
{
    [TestFixture]
    public class ThresholdLoggingTracerTests
    {
        [Test, Ignore("only used for local testing")]
        public async Task Test_logging_output()
        {
            var random = new Random();
            var tracer = new ThresholdLoggingTracer
            {
                Interval = 100,
                KvThreshold = 50
            };

            foreach (var i in Enumerable.Range(1, 100))
            {
                var parentSpan = tracer.BuildSpan("get")
                    .WithTag(CouchbaseTags.OperationId, $"0x{i}")
                    .WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceKv)
                    .Start();

                using (tracer.BuildSpan(CouchbaseOperationNames.RequestEncoding)
                    .AsChildOf(parentSpan)
                    .Start())
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1, 10)));
                }

                using (var dispatchSpan = tracer.BuildSpan(CouchbaseOperationNames.DispatchToServer)
                    .AsChildOf(parentSpan)
                    .Start())
                {
                    dispatchSpan.SetPeerLatencyTag(random.Next(10, 100));
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1, 10)));
                }

                using (tracer.BuildSpan(CouchbaseOperationNames.ResponseDecoding).AsChildOf(parentSpan).Start())
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(random.Next(1, 10)));
                }

                await Task.Delay(TimeSpan.FromMilliseconds(10));
                parentSpan.Finish();

                await Task.Delay(10);
            }

            await Task.Delay(5000);
        }
    }
}
