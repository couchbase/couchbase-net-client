using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Xunit;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class RequestTracerTests
    {
        // RFC 67: every span is CLIENT, including a span with no parent.
        [Fact]
        public void RequestSpan_Without_Parent_Is_Client()
        {
            var name = "span-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();

            // Disposed, and limited to the SDK's source: a registered listener is process-wide.
            using var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RequestTracer.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => stopped.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(listener);

            using var tracer = new RequestTracer();
            tracer.RequestSpan(name).Dispose();

            var activity = Assert.Single(stopped, a => a.OperationName == name);
            Assert.Equal(ActivityKind.Client, activity.Kind);
        }

        // A child copies its tags to its parent when it ends. Through the parent's wrapper, db.statement was mapped
        // again, so the parent got db.query.text for a statement without parameters (RFC 67).
        [Fact]
        public void Statement_Without_Parameters_Does_Not_Reach_Parent_As_Query_Text()
        {
            var name = "parent-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = Listen(stopped);
            using var tracer = new RequestTracer();

            var parent = new RequestSpanWrapper(tracer.RequestSpan(name), convention: ObservabilitySemanticConvention.Both);
            var query = new RequestSpanWrapper(tracer.RequestSpan(OuterRequestSpans.ServiceSpan.N1QLQuery, parent),
                convention: ObservabilitySemanticConvention.Both);
            query.WithStatement("SELECT 1", hasParameters: false);
            query.Dispose();
            parent.Dispose();

            var tags = Assert.Single(stopped, a => a.OperationName == name).TagObjects.Select(t => t.Key).ToList();
            Assert.Contains(OuterRequestSpans.Attributes.Statement, tags);
            Assert.DoesNotContain("db.query.text", tags);
        }

        // A range scan sends one request for each batch. If each request copied its tags up, the partition and
        // range_scan spans would hold tags in proportion to the number of batches until the scan ends.
        [Fact]
        public void Range_Scan_Request_Spans_Do_Not_Copy_Tags_To_Their_Parents()
        {
            var name = "range_scan-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = Listen(stopped);
            using var tracer = new RequestTracer();

            var scan = tracer.RequestSpan(name);
            var partition = tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanPartition, scan);
            partition.SetAttribute("partition_id", 3u);
            for (var i = 0; i < 10; i++)
            {
                using var scanContinue = tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.RangeScanContinue, partition);
                scanContinue.SetAttribute("range_scan_id", "0x01");
                tracer.RequestSpan(InnerRequestSpans.DispatchSpan.Name, scanContinue).Dispose();
            }
            partition.Dispose();
            scan.Dispose();

            var partitionId = partition.Id;
            var partitionTags = Assert.Single(stopped, a => a.Id == partitionId).TagObjects.Select(t => t.Key).ToList();
            Assert.Equal(new[] { "partition_id" }, partitionTags);
            var scanTags = Assert.Single(stopped, a => a.OperationName == name).TagObjects.Select(t => t.Key).ToList();
            Assert.DoesNotContain("partition_id", scanTags);
            Assert.DoesNotContain(ThresholdTags.DispatchDurationTag, scanTags);

            // A dispatch still copies its duration to its request span.
            var continueTags = stopped.Where(a => a.OperationName == OuterRequestSpans.ServiceSpan.Kv.RangeScanContinue
                                                  && a.ParentId == partitionId).ToList();
            Assert.Equal(10, continueTags.Count);
            Assert.All(continueTags, a => Assert.Contains(a.TagObjects, t => t.Key == ThresholdTags.DispatchDurationTag));
        }

        // Other spans still copy their tags up, for the threshold log.
        [Fact]
        public void Other_Spans_Copy_Their_Tags_To_Their_Parents()
        {
            var name = "parent-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = Listen(stopped);
            using var tracer = new RequestTracer();

            var parent = tracer.RequestSpan(name);
            var get = tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Kv.Get, parent);
            tracer.RequestSpan(InnerRequestSpans.DispatchSpan.Name, get).Dispose();
            get.Dispose();
            parent.Dispose();

            Assert.Contains(Assert.Single(stopped, a => a.OperationName == name).TagObjects,
                t => t.Key == ThresholdTags.DispatchDurationTag);
        }

        // Disposed, and limited to the SDK's source: a registered listener is process-wide.
        private static ActivityListener Listen(ConcurrentQueue<Activity> stopped)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RequestTracer.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => stopped.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }
}
