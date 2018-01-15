using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Couchbase.Tracing;
using NUnit.Framework;

namespace Couchbase.UnitTests.Tracing
{
    [TestFixture]
    public class ThresholdLoggingTracerTests
    {
        [Test]
        public void Can_Add_Span()
        {
            var tracer = new ThresholdLoggingTracer();
            var span = new Span(tracer, "operation", null, Stopwatch.GetTimestamp(), null, null);
            span.Tags.Add(CouchbaseTags.Service, CouchbaseTags.ServiceKv);

            Assert.AreEqual(0, tracer.QueuedSpansCount);

            tracer.ReportSpan(span);
            Assert.AreEqual(1, tracer.QueuedSpansCount);
        }

        [Test]
        public void Spans_Without_Service_Tag_Are_Not_Queued()
        {
            var tracer = new ThresholdLoggingTracer();
            var span = new Span(tracer, "operation", null, Stopwatch.GetTimestamp(), null, null);

            Assert.AreEqual(0, tracer.QueuedSpansCount);

            tracer.ReportSpan(span);
            Assert.AreEqual(0, tracer.QueuedSpansCount);
        }

        [Test]
        public void Spans_With_Ignore_Tag_Are_Not_Queued()
        {
            var tracer = new ThresholdLoggingTracer();
            var span = new Span(tracer, "operation", null, Stopwatch.GetTimestamp(), null, null);
            span.Tags.Add(CouchbaseTags.Ignore, true);

            Assert.AreEqual(0, tracer.QueuedSpansCount);

            tracer.ReportSpan(span);
            Assert.AreEqual(0, tracer.QueuedSpansCount);
        }

        [Test]
        public void Spans_Are_Processed_After_Some_Time()
        {
            var tracer = new ThresholdLoggingTracer(500, 10, new Dictionary<string, int>
            {
                {"kv", 100}
            });
            var span = new Span(tracer, "operation", null, Stopwatch.GetTimestamp(), null, null);
            span.Tags.Add(CouchbaseTags.Service, CouchbaseTags.ServiceKv);
            tracer.ReportSpan(span);
            Assert.AreEqual(1, tracer.QueuedSpansCount);

            Thread.Sleep(TimeSpan.FromSeconds(2));

            Assert.AreEqual(0, tracer.QueuedSpansCount);
        }
    }
}
