using System;
using OpenTelemetry.Trace;

namespace Couchbase.Extensions.Tracing.Otel.Tracing
{
    /// <summary>
    /// Couchbase extensions for <see cref="TracerProviderBuilder"/>.
    /// </summary>
    public static class CouchbaseTracerBuilderExtensions
    {
        /// <summary>
        /// Add instrumentation for Couchbase to the tracer.
        /// </summary>
        /// <remarks>
        /// For couchbase2:// connections, this also records the gRPC client span of each operation
        /// (source "Grpc.Net.Client"), named after the gRPC method, as a child of the operation span.
        /// </remarks>
        /// <param name="builder">The <see cref="TracerProviderBuilder" />.</param>
        /// <returns>The <see cref="TracerProviderBuilder" />.</returns>
        public static TracerProviderBuilder AddCouchbaseInstrumentation(this TracerProviderBuilder builder)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure(static (_, deferredBuilder) =>
                {
                    AddSourcesAndProcessor(deferredBuilder);
                });
            }

            return AddSourcesAndProcessor(builder);
        }

        private static TracerProviderBuilder AddSourcesAndProcessor(TracerProviderBuilder builder) =>
            builder
                .AddSource(OpenTelemetryRequestTracer.SourceName, CouchbaseGrpcSpanProcessor.GrpcSourceName)
                .AddProcessor(new CouchbaseGrpcSpanProcessor());
    }
}
