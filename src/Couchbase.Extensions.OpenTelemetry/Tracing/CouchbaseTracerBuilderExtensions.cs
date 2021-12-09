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
        /// <param name="builder">The <see cref="TracerProviderBuilder" />.</param>
        /// <returns>The <see cref="TracerProviderBuilder" />.</returns>
        public static TracerProviderBuilder AddCouchbaseInstrumentation(this TracerProviderBuilder builder)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure(static (_, deferredBuilder) =>
                {
                    deferredBuilder.AddSource(OpenTelemetryRequestTracer.SourceName);
                });
            }

            return builder.AddSource(OpenTelemetryRequestTracer.SourceName);
        }
    }
}
