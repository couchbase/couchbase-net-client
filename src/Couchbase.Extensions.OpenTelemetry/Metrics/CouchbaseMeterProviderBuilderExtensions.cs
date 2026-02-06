using System;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

// ReSharper disable MemberCanBePrivate.Global

// ReSharper disable once CheckNamespace
namespace Couchbase.Extensions.Metrics.Otel
{
    /// <summary>
    /// Couchbase extensions for <see cref="MeterProviderBuilder"/>.
    /// </summary>
    public static class CouchbaseMeterProviderBuilderExtensions
    {
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
        /// <param name="setupAction">Options to configure how meters are instrumented.</param>
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
                });
            }

            AddCouchbaseInstrumentationInternal(builder, options);
            return builder;
        }

        private static void AddCouchbaseInstrumentationInternal(MeterProviderBuilder builder,
            CouchbaseMeterInstrumentationOptions? options)
        {
            // Determine which semantic convention to use
            var convention = options?.SemanticConvention
                ?? ObservabilitySemanticConventionParser.FromEnvironment();

            // Subscribe to the appropriate meter(s) based on the convention
            if (convention is ObservabilitySemanticConvention.Legacy or ObservabilitySemanticConvention.Both)
            {
                builder.AddMeter(CouchbaseMetrics.MeterName);
            }

            if (convention is ObservabilitySemanticConvention.Modern or ObservabilitySemanticConvention.Both)
            {
                builder.AddMeter(CouchbaseMetrics.ModernMeterName);
            }

            if (convention is ObservabilitySemanticConvention.Modern) return;
            // Drop redundant counters if requested (applies only to the legacy meter;
            // the modern meter does not emit these redundant counters)
            if (options is { DropLegacyRedundantCounters: true })
            {
                // The db.couchbase.operations.count metric is intrinsically part of the histogram
                builder.AddView("db.couchbase.operations.count", MetricStreamConfiguration.Drop);

                // The db.couchbase.operations.status metric is included in the "outcome" tag on the histogram
                builder.AddView("db.couchbase.operations.status", MetricStreamConfiguration.Drop);

                // The db.couchbase.timeouts metric is included in the "outcome" tag on the histogram
                builder.AddView("db.couchbase.timeouts", MetricStreamConfiguration.Drop);
            }
        }
    }
}
