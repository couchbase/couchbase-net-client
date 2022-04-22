using System;
using OpenTelemetry.Metrics;

// ReSharper disable once CheckNamespace
namespace Couchbase.Extensions.Metrics.Otel
{
    /// <summary>
    /// Couchbase extensions for <see cref="MeterProviderBuilder"/>.
    /// </summary>
    public static class CouchbaseMeterProviderBuilderExtensions
    {
        private const string CouchbaseMeterName = "CouchbaseNetClient";

        /// <summary>
        /// Add instrumentation for Couchbase to the metric builder.
        /// </summary>
        /// <param name="builder">The <see cref="MeterProviderBuilder" />.</param>
        /// <returns>The <see cref="MeterProviderBuilder" />.</returns>
        public static MeterProviderBuilder AddCouchbaseInstrumentation(this MeterProviderBuilder builder)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure(static (_, deferredBuilder) =>
                {
                    deferredBuilder.AddMeter(CouchbaseMeterName);
                });
            }

            return builder.AddMeter(CouchbaseMeterName);
        }
    }
}
