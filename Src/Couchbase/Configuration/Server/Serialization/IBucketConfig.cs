using System;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal interface IBucketConfig : IEquatable<BucketConfig>
    {
        [JsonProperty("name")]
        string Name { get; set; }

        [JsonProperty("bucketType")]
        string BucketType { get; set; }

        [JsonProperty("authType")]
        string AuthType { get; set; }

        [JsonProperty("saslPassword")]
        string SaslPassword { get; set; }

        [JsonProperty("proxyPort")]
        int ProxyPort { get; set; }

        [JsonProperty("replicaIndex")]
        bool ReplicaIndex { get; set; }

        [JsonProperty("uri")]
        string Uri { get; set; }

        [JsonProperty("streamingUri")]
        string StreamingUri { get; set; }

        [JsonProperty("localRandomKeyUri")]
        string LocalRandomKeyUri { get; set; }

        [JsonProperty("controllers")]
        Controllers Controllers { get; set; }

        [JsonProperty("nodes")]
        Node[] Nodes { get; set; }

        [JsonProperty("stats")]
        Stats Stats { get; set; }

        [JsonProperty("ddocs")]
        Ddocs Ddocs { get; set; }

        [JsonProperty("nodeLocator")]
        string NodeLocator { get; set; }

        [JsonProperty("autoCompactionSettings")]
        bool AutoCompactionSettings { get; set; }

        [JsonProperty("fastWarmupSettings")]
        bool FastWarmupSettings { get; set; }

        [JsonProperty("uuid")]
        string Uuid { get; set; }

        [JsonProperty("vBucketServerMap")]
        VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("replicaNumber")]
        int ReplicaNumber { get; set; }

        [JsonProperty("threadsNumber")]
        int ThreadsNumber { get; set; }

        [JsonProperty("quota")]
        Quota Quota { get; set; }

        [JsonProperty("basicStats")]
        BasicStats BasicStats { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        string[] BucketCapabilities { get; set; }

        [JsonProperty("terseBucketsBase")]
        string TerseUri { get; set; }

        [JsonProperty("terseStreamingBucketsBase")]
        string TerseStreamingUri { get; set; }

        [JsonProperty("rev")]
        int Rev { get; set; }

        string SurrogateHost { get; set; }
    }
}