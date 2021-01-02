using System;
using Couchbase.Utils;
using Newtonsoft.Json;

#nullable enable

namespace Couchbase.Core.Sharding
{
     public sealed class VBucketServerMapDto : IEquatable<VBucketServerMapDto>
     {
        public VBucketServerMapDto()
        {
            HashAlgorithm = string.Empty;
            NumReplicas = 0;
            ServerList = Array.Empty<string>();
            VBucketMap = Array.Empty<short[]>();
            VBucketMapForward = Array.Empty<short[]>();
        }

        [JsonProperty("hashAlgorithm")]
        public string HashAlgorithm { get; set; }

        [JsonProperty("numReplicas")]
        public int NumReplicas { get; set; }

        [JsonProperty("serverList")]
        public string[] ServerList { get; set; }

        [JsonProperty("vBucketMap")]
        public short[][] VBucketMap { get; set; }

        [JsonProperty("vBucketMapForward")]
        public short[][] VBucketMapForward { get; set; }

        public bool Equals(VBucketServerMapDto? other)
        {
            return (other != null
                    && ServerList.AreEqual<string>(other.ServerList)
                    && VBucketMap.AreEqual(other.VBucketMap))
                    && VBucketMapForward.AreEqual(other.VBucketMapForward);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as VBucketServerMapDto);
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
