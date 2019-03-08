using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Core.Sharding
{
     public sealed class VBucketServerMap : IEquatable<VBucketServerMap>
    {
        private readonly object _syncObj = new object();
        private List<IPEndPoint> _ipEndPoints = new List<IPEndPoint>();
        public VBucketServerMap()
        {
            HashAlgorithm = string.Empty;
            NumReplicas = 0;
            ServerList = new string[0];
            VBucketMap = new short[0][];
            VBucketMapForward = new short[0][];
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

        [JsonIgnore]
        public List<IPEndPoint> IPEndPoints
        {
            get
            {
                EnsureIPEndPointsAreLoaded();
                return _ipEndPoints;
            }
        }

        public bool Equals(VBucketServerMap other)
        {
            return (other != null
                    && ServerList.AreEqual<string>(other.ServerList)
                    && VBucketMap.AreEqual<short>(other.VBucketMap))
                    && VBucketMapForward.AreEqual<short>(other.VBucketMapForward);
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

        // ReSharper disable once InconsistentNaming
        private void EnsureIPEndPointsAreLoaded()
        {
            lock (_syncObj)
            {
                if (_ipEndPoints == null || !_ipEndPoints.Any())
                {
                    _ipEndPoints = new List<IPEndPoint>();
                    foreach (var server in ServerList)
                    {
                        _ipEndPoints.Add(IpEndPointExtensions.GetEndPoint(server));
                    }
                }
            }
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            // If we're deserializing the configuration, go ahead and load the endpoints in advance
            // https://issues.couchbase.com/browse/NCBC-1614
            EnsureIPEndPointsAreLoaded();
        }
    }
}
