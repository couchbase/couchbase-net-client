#nullable enable
using System.Collections.Generic;
using System.Diagnostics;
using Couchbase.Extensions.Tracing.Otel.Tracing;
using Couchbase.Test.Common;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace Couchbase.Extensions.Tracing.Otel.UnitTests
{
    [Collection(NonParallelDefinition.Name)]
    public class CouchbaseGrpcSpanProcessorTests
    {
        // Stands in for Grpc.Net.Client, which starts "Grpc.Net.Client.GrpcOut" spans from a source
        // of this name and sets grpc.method when the span starts.
        private static readonly ActivitySource GrpcSource = new(CouchbaseGrpcSpanProcessor.GrpcSourceName);
        private static readonly ActivitySource OtherSource = new(nameof(CouchbaseGrpcSpanProcessorTests));

        private static Activity? StartGrpcCall(string method) =>
            GrpcSource.StartActivity("Grpc.Net.Client.GrpcOut", ActivityKind.Client, parentContext: default,
                tags: new[] { new KeyValuePair<string, object?>("grpc.method", method) });

        [Fact]
        public void GrpcSpan_UnderCouchbaseSpan_IsExported_AndNamedAfterMethod()
        {
            var exported = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exported)
                .Build();
            using var tracer = new OpenTelemetryRequestTracer();

            string? parentSpanId;
            using (var opSpan = tracer.RequestSpan("query"))
            {
                parentSpanId = Activity.Current?.SpanId.ToHexString();
                StartGrpcCall("/couchbase.query.v1.QueryService/Query")?.Dispose();
            }

            var grpcSpan = Assert.Single(exported, a => a.Source.Name == CouchbaseGrpcSpanProcessor.GrpcSourceName);
            Assert.Equal("couchbase.query.v1.QueryService/Query", grpcSpan.DisplayName);
            Assert.Equal(parentSpanId, grpcSpan.ParentSpanId.ToHexString());
            Assert.Equal(ActivityKind.Client, grpcSpan.Kind);
        }

        [Fact]
        public void GrpcSpan_NotUnderCouchbaseSpan_IsNotRenamed()
        {
            var exported = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddSource(OtherSource.Name)
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exported)
                .Build();

            using (OtherSource.StartActivity("app-operation"))
            {
                StartGrpcCall("/my.app.v1.MyService/Call")?.Dispose();
            }
            StartGrpcCall("/my.app.v1.MyService/Call")?.Dispose();

            var grpcSpans = exported.FindAll(a => a.Source.Name == CouchbaseGrpcSpanProcessor.GrpcSourceName);
            Assert.Equal(2, grpcSpans.Count);
            Assert.All(grpcSpans, a => Assert.Equal("Grpc.Net.Client.GrpcOut", a.DisplayName));
        }

        [Fact]
        public void GrpcSpan_WithoutMethodTag_KeepsItsName()
        {
            var exported = new List<Activity>();
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddCouchbaseInstrumentation()
                .AddInMemoryExporter(exported)
                .Build();
            using var tracer = new OpenTelemetryRequestTracer();

            using (tracer.RequestSpan("get"))
            {
                GrpcSource.StartActivity("Grpc.Net.Client.GrpcOut", ActivityKind.Client)?.Dispose();
            }

            var grpcSpan = Assert.Single(exported, a => a.Source.Name == CouchbaseGrpcSpanProcessor.GrpcSourceName);
            Assert.Equal("Grpc.Net.Client.GrpcOut", grpcSpan.DisplayName);
        }
    }
}
