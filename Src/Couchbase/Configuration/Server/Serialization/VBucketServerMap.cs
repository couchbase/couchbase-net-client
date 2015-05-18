using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Serialization
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
            VBucketMap = new int[0][];
            VBucketMapForward = new int[0][];
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

        [JsonIgnore]
        public List<IPEndPoint> IPEndPoints
        {
            get
            {
                if (_ipEndPoints == null || !_ipEndPoints.Any())
                {
                    lock (_syncObj)
                    {
                        _ipEndPoints = new List<IPEndPoint>();
                        foreach (var server in ServerList)
                        {
                            _ipEndPoints.Add(IPEndPointExtensions.GetEndPoint(server));
                        }
                    }
                }
                return _ipEndPoints;
            }
        }

        public bool Equals(VBucketServerMap other)
        {
            return (other != null
                    && ServerList.AreEqual<string>(other.ServerList)
                    && VBucketMap.AreEqual(other.VBucketMap))
                    && VBucketMapForward.AreEqual(other.VBucketMapForward);
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