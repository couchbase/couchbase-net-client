using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.UnitTests.Core.Diagnostics.Metrics;
using Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes;
using Couchbase.UnitTests.Core.Utils;
using Couchbase.UnitTests.Helpers;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using IRequestSpan = Couchbase.Core.Diagnostics.Tracing.IRequestSpan;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing
{
    public class ThresholdTracerTests : IDisposable
    {
        private readonly ITestOutputHelper _testOutputHelper;
        private readonly LoggerFactory _loggerFactory;

        public ThresholdTracerTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(new XUnitLoggerProvider(testOutputHelper));
        }

        public void Dispose()
        {
            _loggerFactory.Dispose();
        }

        [Fact]
        public void Test()
        {
            using var tracer = new RequestTracer();
            using var listener = new XUnitLoggerListener(_loggerFactory.CreateLogger<ThresholdTracerTests>());
            tracer.Start(listener);

            using var parentSpan = tracer.RequestSpan("get");
            using var child = tracer.RequestSpan("get-dispatch", parentSpan);

            var foo = new Foo(parentSpan)
            {
                Bar = new Bar(child)
            };

            foo.WriteTags();
            foo.Bar.WriteTags();
        }

        [Fact]
        public void Test2()
        {
            using var tracer = new RequestTracer();
            using var listener = new ThresholdTraceListener(_loggerFactory, new ThresholdOptions());
            tracer.Start(listener);

            using var parentSpan = tracer.RequestSpan("get");
            using var child = tracer.RequestSpan("get-dispatch", parentSpan);

            var foo = new Foo(parentSpan)
            {
                Bar = new Bar(child)
            };

            foo.WriteTags();
            foo.Bar.WriteTags();
        }

        [Fact]
        public async Task TestKeyValueTracing()
        {
            var cluster = new FakeCluster(new ClusterOptions
            {
                TracingOptions = new TracingOptions {
                   RequestTracer = new RequestTracer().Start(
                        new ThresholdTraceListener(_loggerFactory, new ThresholdOptions()))
                }
            });
            var bucket = await cluster.BucketAsync("fakeBucket");
            var collection = await bucket.DefaultCollectionAsync();

            //await collection.GetAsync("key");
            await collection.GetAnyReplicaAsync("key");
            Assert.NotNull(collection);
        }

        private static ActivitySource source = new ActivitySource("mine", "0.0.1");

        [Fact]
        public void TestChild2()
        {
            var listener = new ActivityListener {
                ActivityStopped = activity =>
                {
                },
                ShouldListenTo = s => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) =>
                    ActivitySamplingResult.AllData
        };
            ActivitySource.AddActivityListener(listener);

            var p2 =source.StartActivity("p2");
            var c2 = source.StartActivity("c2");
        }

        [Fact]
        public void TestChild()
        {
            var parent = new Activity("p1");
            parent.Start();
            var child = new Activity("c1");
            child.Start();
        }

        [Fact]
        public async Task Test_Logs()
        {
            var loggerFactory = new LoggingMeterTests.LoggingMeterTestFactory();
            using var tracer = new RequestTracer();
            using var listener = new ThresholdTraceListener(loggerFactory, new ThresholdOptions
            {
                EmitInterval = TimeSpan.FromMilliseconds(10),
                KvThreshold = TimeSpan.Zero
            });
            tracer.Start(listener);

            using (var parentSpan = tracer.RequestSpan("get"))
            {
                parentSpan.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Kv.Name);
            }

            string report = null;
            // Use async polling instead of SpinWait to avoid starving background tasks on overloaded CI
            var finished = await AsyncTestHelper.WaitForConditionAsync(
                () => loggerFactory.LoggedData.TryTake(out report),
                timeout: TimeSpan.FromSeconds(30));
            Assert.True(finished, userMessage: "Did not find a log entry for threshold data.");
            Assert.NotNull(report);
            Assert.StartsWith("{", report);

            _testOutputHelper.WriteLine(report);
        }

        public interface ITraceable
        {
            IRequestSpan Span { get; set; }
            void WriteTags();
        }

        public class Foo : ITraceable
        {
            public Foo(IRequestSpan span)
            {
                Span = span;
            }

            public Bar Bar { get; set; }

            public void WriteTags()
            {
                Span.SetAttribute("start", DateTime.Now.ToShortTimeString());
                Span.SetAttribute("description", "string data for foo [parent]");
                Span.SetAttribute("age", 2.ToString());
                Span.SetAttribute("vaccinated", false.ToString());
            }

            public IRequestSpan Span { get; set; }
        }

        public class Bar : ITraceable
        {
            public Bar(IRequestSpan span)
            {
                Span = span;
            }

            public void WriteTags()
            {
                Span.SetAttribute("start", DateTime.Now.ToShortTimeString());
                Span.SetAttribute("description", "string data for bar [child]");
                Span.SetAttribute("age", 1.ToString());
                Span.SetAttribute("vaccinated", true.ToString());
            }

            public IRequestSpan Span { get; set; }
        }
    }
}
