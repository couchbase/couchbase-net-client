using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Couchbase.Extensions.Tracing.Otel.UnitTests
{
    public class OpenTelemetryRequestTracerTests
    {
        private static readonly ActivitySource TestSource = new(nameof(OpenTelemetryRequestTracerTests), "1.0.0");

        [Fact]
        public void RequestSpan_NoParent_RootSpan()
        {
            // Arrange

            var exportedItems = new List<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            using var tracer = new OpenTelemetryRequestTracer();

            // Act

            var span = tracer.RequestSpan("my-span");
            span.Dispose();

            // Assert

            var exportedSpan = exportedItems.Find(p => p.OperationName == "my-span");
            Assert.NotNull(exportedSpan);
            Assert.Null(exportedSpan.Parent);
            Assert.Null(exportedSpan.ParentId);
        }

        [Fact]
        public void RequestSpan_ParentHasNoActivity_RootSpan()
        {
            // Arrange

            var exportedItems = new List<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            using var tracer = new OpenTelemetryRequestTracer();

            var parentSpan = new OpenTelemetryRequestSpan(tracer, null, null);

            // Act

            var span = tracer.RequestSpan("my-span", parentSpan);
            span.Dispose();
            parentSpan.Dispose();

            // Assert

            var exportedSpan = exportedItems.Find(p => p.OperationName == "my-span");
            Assert.NotNull(exportedSpan);
            Assert.Null(exportedSpan.Parent);
            Assert.Null(exportedSpan.ParentId);
        }

        [Fact]
        public void RequestSpan_ParentHasActivity_ChildSpan()
        {
            // Arrange

            var exportedItems = new List<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exportedItems)
                .Build();

            using var tracer = new OpenTelemetryRequestTracer();

            var parentSpan = tracer.RequestSpan("parent");

            // Act

            var span = tracer.RequestSpan("my-span", parentSpan);
            span.Dispose();

            parentSpan.Dispose();

            // Assert

            var exportedSpan = exportedItems.Find(p => p.OperationName == "my-span");
            Assert.NotNull(exportedSpan);
            Assert.NotNull(exportedSpan.ParentId);
        }

        [Fact]
        public void RequestSpan_ParentExternalToCouchbase_ChildSpan()
        {
            // Arrange

            var exportedItems = new List<Activity>();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exportedItems)
                // We must subscribe to the source so it actually creates trace activities
                .AddSource(TestSource.Name)
                .Build();

            using var tracer = new OpenTelemetryRequestTracer();

            var parentActivity = TestSource.StartActivity("parent");

            // Act

            var span = tracer.RequestSpan("my-span");
            span.Dispose();

            parentActivity?.Dispose();

            // Assert

            var exportedSpan = exportedItems.Find(p => p.OperationName == "my-span");
            Assert.NotNull(exportedSpan);
            Assert.Equal(parentActivity?.Id, exportedSpan.ParentId);
        }
    }
}
