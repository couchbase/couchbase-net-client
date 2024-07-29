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
        public bool ExcludeLegacyMetrics { get; set; } = false;
    }
}
