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

        // A query span used to copy all its tags to its parent when it ended. Through the parent's wrapper,
        // db.statement was mapped again, so the parent got db.query.text for a statement without parameters (RFC 67).
        // Now only encoding and dispatch spans copy tags, so the statement does not reach the parent at all.
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
            Assert.DoesNotContain(OuterRequestSpans.Attributes.Statement, tags);
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

        // The threshold log reads the dispatch details from the request span, so a dispatch span copies them to its
        // parent. It copies only those tags.
        [Fact]
        public void Dispatch_Span_Copies_Only_The_Threshold_Tags_To_Its_Request_Span()
        {
            var name = "get-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = Listen(stopped);
            using var tracer = new RequestTracer();

            var get = tracer.RequestSpan(name);
            var dispatch = tracer.RequestSpan(InnerRequestSpans.DispatchSpan.Name, get);
            dispatch.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, "0x2a");
            dispatch.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, "10.0.0.1");
            dispatch.SetAttribute("other", "value");
            dispatch.Dispose();
            get.Dispose();

            var tags = Assert.Single(stopped, a => a.OperationName == name).TagObjects.Select(t => t.Key).ToList();
            Assert.Contains(ThresholdTags.DispatchDurationTag, tags);
            Assert.Contains(InnerRequestSpans.DispatchSpan.Attributes.OperationId, tags);
            Assert.Contains(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, tags);
            Assert.DoesNotContain("other", tags);
        }

        // An operation span does not copy its tags to its parent. get_collection_id runs under the operation that
        // needs the collection ID. If it copied its tags, the threshold log would name that operation
        // get_collection_id, and would add the lookup's dispatch to the operation's dispatches.
        [Fact]
        public void Collection_Id_Lookup_Does_Not_Change_The_Threshold_Entry_Of_Its_Operation()
        {
            var name = "get-" + Guid.NewGuid();
            var stopped = new ConcurrentQueue<Activity>();
            using var listener = Listen(stopped);
            using var tracer = new RequestTracer();

            var get = tracer.RequestSpan(name);
            get.SetAttribute(OuterRequestSpans.Attributes.Operation, OuterRequestSpans.ServiceSpan.Kv.Get);
            var lookup = tracer.RequestSpan(OuterRequestSpans.ServiceSpan.Internal.GetCollectionId, get);
            lookup.SetAttribute(OuterRequestSpans.Attributes.Operation, OuterRequestSpans.ServiceSpan.Internal.GetCollectionId);
            var lookupDispatch = tracer.RequestSpan(InnerRequestSpans.DispatchSpan.Name, lookup);
            lookupDispatch.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, "0x01");
            lookupDispatch.Dispose();
            lookup.Dispose();
            var getDispatch = tracer.RequestSpan(InnerRequestSpans.DispatchSpan.Name, get);
            getDispatch.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.OperationId, "0x02");
            getDispatch.Dispose();
            get.Dispose();

            var summary = ThresholdSummary.FromActivity(Assert.Single(stopped, a => a.OperationName == name));
            Assert.Equal(OuterRequestSpans.ServiceSpan.Kv.Get, summary.operation_name);
            Assert.Equal("0x02", summary.operation_id);
            Assert.Equal(summary.last_dispatch_duration_us, summary.total_dispatch_duration_us);
        }

        // Disposed, and limited to the SDK's source: a registered listener is process-wide.
        // SampleUsingParentId is needed for a span whose parent is a RequestSpanWrapper: the tracer passes the parent's
        // ID as a string. On .NET Framework that ID is hierarchical, and only this callback is asked about it, so
        // without it the span is not sampled unless another test left a listener that samples it.
        private static ActivityListener Listen(ConcurrentQueue<Activity> stopped)
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == RequestTracer.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => stopped.Enqueue(activity)
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }
}
