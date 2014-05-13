using System;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
{
    internal sealed class VBucketServerMap : IEquatable<VBucketServerMap>
    {
        public VBucketServerMap()
        {
            HashAlgorithm = string.Empty;
            NumReplicas = 0;
            ServerList = new string[0];
            VBucketMap = new int[0][];
        }

        [JsonProperty("hashAlgorithm")]
        public string HashAlgorithm { get; set; }

        [JsonProperty("numReplicas")]
        public int NumReplicas { get; set; }

        [JsonProperty("serverList")]
        public string[] ServerList { get; set; }

        [JsonProperty("vBucketMap")]
        public int[][] VBucketMap { get; set; }

        [JsonProperty("vBucketMapForward")]
        public int[][] VBucketMapForward { get; set; }

        public bool Equals(VBucketServerMap other)
        {
            return (other != null && 
                ServerList.AreEqual<string>(other.ServerList) &&
                VBucketMap.AreEqual(other.VBucketMap));
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as VBucketServerMap);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 23 + ServerList.GetCombinedHashcode();
                hash = hash * 23 + VBucketMap.GetCombinedHashcode();
                hash = hash * 23 + NumReplicas.GetHashCode();
                hash = hash * 23 + HashAlgorithm.GetHashCode();
                return hash;
            }
        }
    }
}