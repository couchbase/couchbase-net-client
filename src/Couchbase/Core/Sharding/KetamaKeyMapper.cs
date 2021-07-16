using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Couchbase.Core.Sharding
{
     /// <summary>
    /// Provides a means of consistent hashing for keys used by Memcached Buckets.
    /// </summary>
    internal class KetamaKeyMapper : IKeyMapper
    {
        private readonly ICollection<IPEndPoint> _servers;
        private readonly int _totalWeight;
        internal readonly SortedDictionary<long, IPEndPoint> Hashes = new SortedDictionary<long, IPEndPoint>();

        public KetamaKeyMapper(IEnumerable<IPEndPoint> servers)
        {
            _servers = servers.ToList();
            _totalWeight = _servers.Count;
            Initialize();
        }

        /// <summary>
        /// Maps a Key to a node in the cluster.
        /// </summary>
        /// <param name="key">The key to map.</param>
        /// <returns>An object representing the node that the key was mapped to, which implements <see cref="IMappedNode"/></returns>
        public IMappedNode MapKey(string key)
        {
            var hash = GetHash(key);
            var index = FindIndex(hash);
            var server = Hashes[Hashes.Keys.ToList()[index]];
            return new KetamaNode(server);
        }

        /// <summary>
        /// Not Supported: This overload is only supported by Couchbase buckets.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="notMyVBucket"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key, bool notMyVBucket)
        {
            throw new NotSupportedException("This overload is only supported by Couchbase buckets.");
        }

        /// <summary>
        /// Finds the index of a node for a given key.
        /// </summary>
        /// <param name="key">The Key that the index belongs to.</param>
        /// <returns>The index of key - which is the location of the node that the key maps to.</returns>
        public int FindIndex(long key)
        {
            var index = Array.BinarySearch(Hashes.Keys.ToArray(), key);
            if (index < 0)
            {
                index = ~index;
                if (index == 0)
                {
                    index = Hashes.Keys.Count() - 1;
                }
                else if (index >= Hashes.Count())
                {
                    index = 0;
                }
            }
            if (index < 0 || index > Hashes.Count())
            {
                throw new InvalidOperationException();
            }
            return index;
        }

        /// <summary>
        /// Creates a hash for a given Key.
        /// </summary>
        /// <param name="key">The Key to hash.</param>
        /// <returns>A hash of the Key.</returns>
        public long GetHash(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            using (var md5 = MD5.Create())
            {
                var hash = md5.ComputeHash(bytes);
                var result = ((long) (hash[3] & 0xFF) << 24)
                             | ((long) (hash[2] & 0xFF) << 16)
                             | ((long) (hash[1] & 0xFF) << 8)
                             | (uint) hash[0] & 0xFF;
                return result;
            }
        }

        /// <summary>
        /// Initializes the mapping of hashes to nodes.
        /// </summary>
        public void Initialize()
        {
            using (var md5 = MD5.Create())
            {
                foreach (var server in _servers)
                {
                    for (var rep = 0; rep < 40; rep++)
                    {
                        var bytes = Encoding.UTF8.GetBytes($"{server}-{rep}");
                        var hash = md5.ComputeHash(bytes);
                        for (var j = 0; j < 4; j++)
                        {
                            var key = ((long) (hash[3 + j * 4] & 0xFF) << 24)
                                      | ((long) (hash[2 + j * 4] & 0xFF) << 16)
                                      | ((long) (hash[1 + j * 4] & 0xFF) << 8)
                                      | (uint) (hash[0 + j * 4] & 0xFF);

                            Hashes[key] = server;
                        }
                    }
                }
            }
        }

        public ulong Rev { get; set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
