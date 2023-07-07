using System.Collections.Generic;
using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal class OrphanServiceReport
    {
        [JsonPropertyName("total_count")]
        public uint TotalCount { get; set; }

        [JsonPropertyName("top_requests")]
        public OrphanSummary[]? TopRequests { get; set; }
    }
}
