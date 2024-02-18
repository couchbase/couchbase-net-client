#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    internal readonly struct HistogramData(int totalCount, PercentilesUs percentiles)
    {
        public int TotalCount { get; } = totalCount;
        public PercentilesUs Percentiles { get; } = percentiles;
    }
}
