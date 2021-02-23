#nullable enable


// ReSharper disable InconsistentNaming
// keep it simple for JSON
namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    internal readonly struct ThresholdSummaryReport
    {
        public readonly string service;
        public readonly int count;
        public readonly ThresholdSummary[] top;

        public ThresholdSummaryReport(string service, int count, ThresholdSummary[] top)
            => (this.service, this.count, this.top) = (service, count, top);

    }
}
