using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Common.Logging;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Cryptography;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Provides a means of mapping keys to nodes within a Couchbase Server and a Couchbase Bucket.
    /// </summary>
    internal class VBucketKeyMapper : IKeyMapper
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly int _mask = 1023;
        private readonly Dictionary<int, IVBucket> _vBuckets;
        private readonly Dictionary<int, IVBucket> _vForwardBuckets;
        private readonly VBucketServerMap _vBucketServerMap;
        private readonly List<IServer> _servers;

        public VBucketKeyMapper(List<IServer> servers, VBucketServerMap vBucketServerMap)
        {
            _servers = servers;
            _vBucketServerMap = vBucketServerMap;
            _vBuckets = CreateVBucketMap();
            _vForwardBuckets = CreateVBucketMapForwards();
            _mask = _vBuckets.Count-1;
        }

        /// <summary>
        /// Maps a given Key to it's node in a Couchbase Cluster.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key)
        {
            var index = GetIndex(key);
            Log.Info(m=>m("Using index {0} for key {1} - rev{2}", index, key, Rev));
            return _vBuckets[index];
        }

        public int GetIndex(string key)
        {
            using (var crc32 = new Crc32())
            {
                var keyBytes = Encoding.UTF8.GetBytes(key);
                var hashedKeyBytes = crc32.ComputeHash(keyBytes);
                var hash = BitConverter.ToUInt32(hashedKeyBytes, 0);
                return (int)hash & _mask;
            }
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<int, IVBucket> CreateVBucketMap()
        {
            var vBuckets = new Dictionary<int, IVBucket>();
            var vBucketForwardMap = _vBucketServerMap.VBucketMapForward;
            var vBucketMap = _vBucketServerMap.VBucketMap;

            Log.Info(m => m("Creating VBuckets {0} and FMaps {1} for Rev#{2}", vBucketMap.Length,
                vBucketForwardMap == null ? 0: vBucketForwardMap.Length, Rev));

            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var primary = vBucketMap[i][0];
                var replicas = new int[vBucketMap[i].Length-1];
                for (var r = 1; r < vBucketMap[i].Length; r++)
                {
                    replicas[r - 1] = vBucketMap[i][r];
                }
                vBuckets.Add(i, new VBucket(_servers, i, primary, replicas));
            }
            return vBuckets;
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<int, IVBucket> CreateVBucketMapForwards()
        {
            var vBucketMapForwards = new Dictionary<int, IVBucket>();
            var vBucketMapForward = _vBucketServerMap.VBucketMapForward;

            if (vBucketMapForward != null)
            {
                Log.Info(m => m("Creating VBucketMapForwards {0}", vBucketMapForward.Length));

                for (var i = 0; i < vBucketMapForward.Length; i++)
                {
                    var primary = vBucketMapForward[i][0];
                    var replicas = new int[vBucketMapForward[i].Length-1];
                    for (var r = 1; r < vBucketMapForward[i].Length; r++)
                    {
                        replicas[r - 1] = vBucketMapForward[i][r];
                    }
                    vBucketMapForwards.Add(i, new VBucket(_servers, i, primary, replicas));
                }
            }
            return vBucketMapForwards;
        }

        internal Dictionary<int, IVBucket> GetVBuckets()
        {
            return _vBuckets;
        }

        internal Dictionary<int, IVBucket> GetVBucketsForwards()
        {
            return _vForwardBuckets;
        }

        public int Rev { get; set; }
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
