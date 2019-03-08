using Couchbase.Core.Sharding;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Couchbase.Core.Configuration.Server
{
    public class Ports
    {
        public int direct { get; set; }
    }

    public class Node
    {
        public string couchApiBase { get; set; }
        public string hostname { get; set; }
        public Ports ports { get; set; }
    }

    public class Services
    {
        public int mgmt { get; set; }
        public int mgmtSSL { get; set; }
        public int indexAdmin { get; set; }
        public int indexScan { get; set; }
        public int indexHttp { get; set; }
        public int indexStreamInit { get; set; }
        public int indexStreamCatchup { get; set; }
        public int indexStreamMaint { get; set; }
        public int indexHttps { get; set; }
        public int kv { get; set; }
        public int kvSSL { get; set; }
        public int capi { get; set; }
        public int capiSSL { get; set; }
        public int projector { get; set; }
        public int n1ql { get; set; }
        public int n1qlSSL { get; set; }
    }

    public class NodesExt
    {
        public Services services { get; set; }
        public bool thisNode { get; set; }
    }

    public class Ddocs
    {
        public string uri { get; set; }
    }

    //use Couchbase.Core.ShardingVBucketServerMap instead
   /* public class VBucketServerMap
    {
        public string hashAlgorithm { get; set; }
        public int numReplicas { get; set; }
        public List<string> serverList { get; set; }
        public List<List<int>> vBucketMap { get; set; }
    }*/

    //Root object
    public class BucketConfig
    {
        [JsonProperty("rev")]
        public uint Rev { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("streamingUri")]
        public string StreamingUri { get; set; }

        [JsonProperty("nodes")]
        public List<Node> Nodes { get; set; }

        [JsonProperty("nodesExt")]
        public List<NodesExt> NodesExt { get; set; }

        [JsonProperty("nodeLocator")]
        public string NodeLocator { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("ddocs")]
        public Ddocs Ddocs { get; set; }

        [JsonProperty("vBucketServerMap")]
        public VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        public string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        public List<string> BucketCapabilities { get; set; }
    }
}
