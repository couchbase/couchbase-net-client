using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Buckets
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("streamingUri")]
        public string StreamingUri { get; set; }

        [JsonProperty("terseBucketsBase")]
        public string TerseBucketsBase { get; set; }

        [JsonProperty("terseStreamingBucketsBase")]
        public string TerseStreamingBucketsBase { get; set; }
    }
}
