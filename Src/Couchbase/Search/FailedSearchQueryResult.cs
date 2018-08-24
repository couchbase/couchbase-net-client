using Newtonsoft.Json;

namespace Couchbase.Search
{
    internal class FailedSearchQueryResult
    {
        [JsonProperty("error")]
        internal string Message { get; set; }

        [JsonProperty("status")]
        internal string Status { get; set; }
    }
}
