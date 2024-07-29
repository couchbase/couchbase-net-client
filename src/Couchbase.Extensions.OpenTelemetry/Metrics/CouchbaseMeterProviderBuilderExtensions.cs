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
        public static MeterProviderBuilder AddCouchbaseInstrumentation(this MeterProviderBuilder builder) =>
            builder.AddCouchbaseInstrumentation(null);

        /// <summary>
        /// Add instrumentation for Couchbase to the metric builder.
        /// </summary>
        /// <param name="builder">The <see cref="MeterProviderBuilder" />.</param>
        /// <param name="options">Options to configure how meters are instrumented.</param>
        /// <returns>The <see cref="MeterProviderBuilder" />.</returns>
        public static MeterProviderBuilder AddCouchbaseInstrumentation(this MeterProviderBuilder builder, Action<CouchbaseMeterInstrumentationOptions>? setupAction)
        {
            CouchbaseMeterInstrumentationOptions? options = null;
            if (setupAction is not null)
            {
                options = new CouchbaseMeterInstrumentationOptions();
                setupAction(options);
            }

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((_, deferredBuilder) =>
                {
                    AddCouchbaseInstrumentationInternal(deferredBuilder, options);
                    deferredBuilder.AddMeter(CouchbaseMeterName);
                });
            }

            AddCouchbaseInstrumentationInternal(builder, options);
            return builder;
        }

        private static void AddCouchbaseInstrumentationInternal(MeterProviderBuilder builder,
            CouchbaseMeterInstrumentationOptions? options)
        {
            builder.AddMeter(CouchbaseMeterName);

            // If options is null use the default behavior of including legacy metrics
            if (options is { ExcludeLegacyMetrics: true })
            {
                // The db.couchbase.operations.count metric is intrinsically part of the db.couchbase.operations histogram
                builder.AddView("db.couchbase.operations.count", MetricStreamConfiguration.Drop);

                // The db.couchbase.operations.status metric is included in the "outcome" tag on the db.couchbase.operations histogram
                builder.AddView("db.couchbase.operations.status", MetricStreamConfiguration.Drop);

                // The db.couchbase.timeouts metric is included in the "outcome" tag on the db.couchbase.operations histogram
                builder.AddView("db.couchbase.timeouts", MetricStreamConfiguration.Drop);
            }
        }
    }
}
