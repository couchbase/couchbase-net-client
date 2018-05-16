using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Tracing
{
    internal class OperationContext
    {
        [JsonProperty("s")]
        public string ServiceType { get; }

        [JsonProperty("i")]
        public string CorrelationId { get; }

        [JsonProperty("b", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string BucketName { get; set; }

        [JsonProperty("l", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string LocalEndpoint { get; set; }

        [JsonProperty("r", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string RemoteEndpoint { get; set; }

        [JsonProperty("t", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int TimeoutMicroseconds { get; set; }

        public OperationContext(string serviceType, string correlationId = null)
        {
            ServiceType = serviceType;
            CorrelationId = correlationId;
        }

        public override string ToString()
        {
            return string.Join(" ",
                ExceptionUtil.OperationTimeout,
                JsonConvert.SerializeObject(this, Formatting.None)
            );
        }
    }
}
