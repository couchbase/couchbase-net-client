using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Couchbase.Diagnostics
{
    internal class EndpointDiagnostics : IEndpointDiagnostics
    {
        [JsonIgnore]
        public ServiceType Type { get; internal set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; internal set; }

        [JsonProperty("state", NullValueHandling = NullValueHandling.Ignore)]
        [JsonConverter(typeof(StringEnumConverter))]
        public ServiceState? State { get; internal set; }

        [JsonProperty("local", NullValueHandling = NullValueHandling.Ignore)]
        public string Local { get; internal set; }

        [JsonProperty("remote", NullValueHandling = NullValueHandling.Ignore)]
        public string Remote { get; internal set; }

        [JsonProperty("last_activity_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? LastActivity { get; internal set; }

        [JsonProperty("latency_us", NullValueHandling = NullValueHandling.Ignore)]
        public long? Latency { get; internal set; }

        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
        public string Scope { get; internal set; }

        public void SetLatency(long latency)
        {
            Latency = latency / 10;
        }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
