using System.Text.Json.Serialization;

#nullable enable

namespace Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting
{
    internal class OrphanReport
    {
        [JsonPropertyName(OuterRequestSpans.ServiceSpan.Kv.Name)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OrphanServiceReport? KeyValue;

        [JsonPropertyName(OuterRequestSpans.ServiceSpan.ViewQuery)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OrphanServiceReport? ViewQuery;

        [JsonPropertyName(OuterRequestSpans.ServiceSpan.N1QLQuery)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OrphanServiceReport? N1QlQuery;

        [JsonPropertyName(OuterRequestSpans.ServiceSpan.SearchQuery)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OrphanServiceReport? SearchQuery;

        [JsonPropertyName(OuterRequestSpans.ServiceSpan.AnalyticsQuery)]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public OrphanServiceReport? AnalyticsQuery;

        [JsonIgnore]
        public bool HasReports =>
            KeyValue is not null
            || ViewQuery is not null
            || N1QlQuery is not null
            || SearchQuery is not null
            || AnalyticsQuery is not null;
    }
}
