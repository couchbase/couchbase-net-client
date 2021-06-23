 using Newtonsoft.Json;

#nullable enable


// ReSharper disable InconsistentNaming
// keep it simple for JSON
namespace Couchbase.Core.Diagnostics.Tracing.ThresholdTracing
{
    internal readonly struct ThresholdSummaryReport
    {
        [JsonIgnore]
        public readonly string service;
        public readonly int total_count;
        public readonly ThresholdSummary[] top_requests;

        public ThresholdSummaryReport(string service, int count, ThresholdSummary[] top)
            => (this.service, this.total_count, this.top_requests) = (service, count, top);

    }
}
