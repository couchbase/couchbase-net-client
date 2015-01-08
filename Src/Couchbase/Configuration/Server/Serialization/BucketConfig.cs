using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    [JsonObject("bucket")]
    public sealed class BucketConfig : IBucketConfig
    {
        private string _surrogateHost = "127.0.0.1";

        public BucketConfig()
        {
            VBucketServerMap = new VBucketServerMap();
        }

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

        [JsonProperty("terseBucketsBase")]
        public string TerseUri { get; set; }

        [JsonProperty("terseStreamingBucketsBase")]
        public string TerseStreamingUri { get; set; }

        [JsonProperty("localRandomKeyUri")]
        public string LocalRandomKeyUri { get; set; }

        [JsonProperty("controllers")]
        public Controllers Controllers { get; set; }

        [JsonProperty("nodes")]
        public Node[] Nodes { get; set; }

        [JsonProperty("nodesExt")]
        public NodeExt[] NodesExt { get; set; }

        [JsonProperty("stats")]
        public Stats Stats { get; set; }

        [JsonProperty("ddocs")]
        public Ddocs Ddocs { get; set; }

        [JsonProperty("nodeLocator")]
        public string NodeLocator { get; set; }

        [JsonProperty("autoCompactionSettings")]
        public AutoCompactionSettings AutoCompactionSettings { get; set; }

        [JsonProperty("fastWarmupSettings")]
        public FastWarmupSettings FastWarmupSettings { get; set; }

        [JsonProperty("uuid")]
        public string Uuid { get; set; }

        [JsonProperty("vBucketServerMap")]
        public VBucketServerMap VBucketServerMap { get; set; }

        [JsonProperty("replicaNumber")]
        public int ReplicaNumber { get; set; }

        [JsonProperty("threadsNumber")]
        public int ThreadsNumber { get; set; }

        [JsonProperty("quota")]
        public Quota Quota { get; set; }

        [JsonProperty("basicStats")]
        public BasicStats BasicStats { get; set; }

        [JsonProperty("bucketCapabilitiesVer")]
        public string BucketCapabilitiesVer { get; set; }

        [JsonProperty("bucketCapabilities")]
        public string[] BucketCapabilities { get; set; }

        [JsonProperty("rev")]
        public int Rev { get; set; }

        public bool UseSsl { get; set; }

        public string SurrogateHost
        {
            get { return _surrogateHost; }
            set { _surrogateHost = value; }
        }

        public string Password { get; set; }

        public Node GetRandomNode()
        {
            Nodes.Shuffle();
            return Nodes.First();
        }

        public bool Equals(BucketConfig other)
        {
            return (other != null &&
                    other.Name == Name &&
                    other.AuthType == AuthType &&
                    other.NodeLocator == NodeLocator &&
                    other.SaslPassword == SaslPassword &&
                    other.Uuid == Uuid &&
                    other.BucketType == BucketType &&
                    other.Nodes.AreEqual<Node>(Nodes) &&
                    other.VBucketServerMap.Equals(VBucketServerMap));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BucketConfig);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash*23 + VBucketServerMap.GetHashCode();
                hash = hash*23 + BucketType.GetHashCode();
                hash = hash*23 + Name.GetHashCode();
                hash = hash*23 + AuthType.GetHashCode();
                hash = hash*23 + NodeLocator.GetHashCode();
                hash = hash*23 + SaslPassword.GetHashCode();
                hash = hash*23 + Uuid.GetHashCode();
                hash = hash*23 + Nodes.GetCombinedHashcode();
                return hash;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("Name={0}", Name);
            return sb.ToString();
        }

        public Uri GetTerseStreamingUri(Node node, bool useSsl)
        {
            var protocol = useSsl ? "https://" : "http://";
            var hostName = node.Hostname;
            var streamingUri = string.IsNullOrEmpty(TerseStreamingUri) ? StreamingUri : TerseStreamingUri;

            if (useSsl)
            {
                hostName = hostName.Replace(((int) DefaultPorts.MgmtApi).ToString(CultureInfo.InvariantCulture),
                    ((int) DefaultPorts.HttpsMgmt).ToString(CultureInfo.InvariantCulture));
            }
            return new Uri(string.Concat(protocol, hostName, streamingUri));
        }

        public Uri GetTerseUri(Node node, bool useSsl)
        {
            var protocol = useSsl ? "https://" : "http://";
            var hostName = node.Hostname;
            var streamingUri = TerseUri;

            if (useSsl)
            {
                hostName = hostName.Replace(((int)DefaultPorts.MgmtApi).ToString(CultureInfo.InvariantCulture),
                    ((int)DefaultPorts.HttpsMgmt).ToString(CultureInfo.InvariantCulture));
            }
            return new Uri(string.Concat(protocol, hostName, streamingUri));
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
