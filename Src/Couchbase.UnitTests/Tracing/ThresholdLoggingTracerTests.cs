using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.IO.Operations;
using Couchbase.Tracing;
using Moq;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class ThresholdLoggingTracerTests
    {
        [Test]
        public void Can_override_default_configuration_values()
        {
            var tracer = new ThresholdLoggingTracer
            {
                KvThreshold = 100,
                ViewThreshold = 110,
                N1qlThreshold = 120,
                SearchThreshold = 130,
                AnalyticsThreshold = 140,
                SampleSize = 150,
                Interval = 160
            };

            Assert.AreEqual(100, tracer.KvThreshold);
            Assert.AreEqual(110, tracer.ViewThreshold);
            Assert.AreEqual(120, tracer.N1qlThreshold);
            Assert.AreEqual(130, tracer.SearchThreshold);
            Assert.AreEqual(140, tracer.AnalyticsThreshold);
            Assert.AreEqual(150, tracer.SampleSize);
            Assert.AreEqual(160, tracer.Interval);
        }

        [Test]
        public async Task Can_add_lots_of_spans_concurrently()
        {
            var tracer = new ThresholdLoggingTracer
            {
                KvThreshold = 1 // really low threshold so all spans are logged
            };

            var tasks = Enumerable.Range(1, 1000).Select(x =>
            {
                var span = tracer.BuildSpan(x.ToString()).WithTag(CouchbaseTags.Service, CouchbaseTags.ServiceKv).Start();
                span.Finish(DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(1));
                return Task.FromResult(true);
            });

            // schedule all the tasks using threadpool
            await Task.WhenAll(tasks);

            // wait for queue to flush
            await Task.Delay(1000);

            // check all items made it into sample
            Assert.AreEqual(1000, tracer.TotalSummaryCount);
        }

        [Test]
        public void Creating_span_with_ignore_flag_is_not_related_to_wrapping_span()
        {
            var tracer = new ThresholdLoggingTracer();
            var mockOperation = new Mock<IOperation>();

            using (var wrappingScope = tracer.BuildSpan("wrapping-span").StartActive())
            {
                using (var scope = tracer.StartParentScope(mockOperation.Object, "bucket", ignoreActiveSpan: true))
                {
                    // should not be the same trace ID as wrapping span
                    Assert.AreNotEqual(wrappingScope.Span.Context.TraceId, scope.Span.Context.TraceId);
                }
            }
        }
    }
}
