using System;
using Couchbase.Core.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Couchbase.Extensions.Metrics.Otel
{
    /// <summary>
    /// Various options which affect how metrics are exported.
    /// </summary>
    public sealed class CouchbaseMeterInstrumentationOptions
    {
        /// <summary>
        /// Set to <c>true</c> to exclude some duplicative legacy metrics. Defaults to <c>false</c>.
        /// </summary>
        [Obsolete("Use DropLegacyRedundantCounters instead.")]
        public bool ExcludeLegacyMetrics
        {
            get => DropLegacyRedundantCounters;
            set => DropLegacyRedundantCounters = value;
        }

        /// <summary>
        /// Set to <c>true</c> to drop legacy meter counters that are redundant with histogram data. Defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// When enabled, the following metrics are dropped via OTel Views:
        /// <list type="bullet">
        ///   <item><description>db.couchbase.operations.count - redundant with histogram count</description></item>
        ///   <item><description>db.couchbase.operations.status - included in outcome tag on histogram</description></item>
        ///   <item><description>db.couchbase.timeouts - included in outcome tag on histogram</description></item>
        /// </list>
        /// This applies only to the legacy meter. The modern meter does not emit these redundant counters.
        /// </remarks>
        public bool DropLegacyRedundantCounters { get; set; } = false;

        /// <summary>
        /// The semantic convention to use for metrics. Determines which meter(s) to subscribe to.
        /// If not set, defaults to reading from the OTEL_SEMCONV_STABILITY_OPT_IN environment variable.
        /// </summary>
        public ObservabilitySemanticConvention? SemanticConvention { get; set; }
    }
}
