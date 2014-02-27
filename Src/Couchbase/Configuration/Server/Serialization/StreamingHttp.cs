using System;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class StreamingHttp
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("bucketType")]
        public string BucketType { get; set; }

        [JsonProperty("authType")]
        public string AuthType { get; set; }

        [JsonProperty("saslPassword")]
        public string SaslPassword { get; set; }

        [JsonProperty("proxyPort")]
        public int ProxyPort { get; set; }

        [JsonProperty("replicaIndex")]
        public bool ReplicaIndex { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("streamingUri")]
        public string StreamingUri { get; set; }

        [JsonProperty("localRandomKeyUri")]
        public string LocalRandomKeyUri { get; set; }

        [JsonProperty("controllers")]
        public Controllers Controllers { get; set; }

        [JsonProperty("nodes")]
        public Node[] Nodes { get; set; }

        [JsonProperty("stats")]
        public Stats Stats { get; set; }

        [JsonProperty("ddocs")]
        public Ddocs Ddocs { get; set; }

        [JsonProperty("nodeLocator")]
        public string NodeLocator { get; set; }

        [JsonProperty("autoCompactionSettings")]
        public bool AutoCompactionSettings { get; set; }

        [JsonProperty("fastWarmupSettings")]
        public bool FastWarmupSettings { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("vBucketServerMap")]
        public VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        public string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        public string[] BucketCapabilities { get; set; }
    }
}