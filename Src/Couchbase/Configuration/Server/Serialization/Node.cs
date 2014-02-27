using System;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class Node : IEquatable<Node>
    {
        [JsonProperty("couchApiBase")]
        public string CouchApiBase { get; set; }

        [JsonProperty("replication")]
        public double Replication { get; set; }

        [JsonProperty("clusterMembership")]
        public string ClusterMembership { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("thisNode")]
        public bool ThisNode { get; set; }

        [JsonProperty("hostname")]
        public string Hostname { get; set; }

        [JsonProperty("clusterCompatibility")]
        public int ClusterCompatibility { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("os")]
        public string Os { get; set; }

        [JsonProperty("ports")]
        public Ports Ports { get; set; }

        public bool Equals(Node other)
        {
            return (other != null &&
                    //other.ClusterMembership == ClusterMembership &&
                    other.Hostname == Hostname);// &&
                    //other.Status == Status);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as Node);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                //hash = hash * 23 + ClusterMembership.GetHashCode();
                hash = hash * 23 + Hostname.GetHashCode();
                //hash = hash * 23 + Status.GetHashCode();
                return hash;
            }
        }
    }
}