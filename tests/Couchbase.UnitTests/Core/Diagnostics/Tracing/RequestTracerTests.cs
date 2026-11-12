using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Couchbase.Core.Diagnostics.Tracing;
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
    }
}
