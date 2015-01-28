﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Couchbase.Core.Buckets
{
    /// <summary>
    /// Provides a means of consistent hashing for keys used by Memcached Buckets.
    /// </summary>
    internal class KetamaKeyMapper : IKeyMapper
    {
        private readonly List<IServer> _servers;
        private readonly int _totalWeight;
        private readonly SortedDictionary<long, IServer> _buckets = new SortedDictionary<long, IServer>();

        public KetamaKeyMapper(List<IServer> servers)
        {
            _servers = servers;
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
            var server = _buckets[_buckets.Keys.ToList()[index]];

            return new KetamaNode(server);
        }

        /// <summary>
        /// Finds the index of a node for a given key.
        /// </summary>
        /// <param name="key">The Key that the index belongs to.</param>
        /// <returns>The index of key - which is the location of the node that the key maps to.</returns>
        public int FindIndex(long key)
        {
            var index = Array.BinarySearch(_buckets.Keys.ToArray(), key);
            if (index < 0)
            {
                index = ~index;
                if (index == 0)
                {
                    index = _buckets.Keys.Count() - 1;
                }
                else if (index >= _buckets.Count())
                {
                    index = 0;
                }
            }
            if (index < 0 || index > _buckets.Count())
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
            foreach (var server in _servers)
            {
                const int weight = 1; //may change this later
                var factor = Math.Floor(40*_servers.Count()*weight/(double) _totalWeight);

                for (long n = 0; n < factor; n++)
                {
                    var bytes = Encoding.UTF8.GetBytes(server.EndPoint + "-" + n);
                    using (var md5 = MD5.Create())
                    {
                        var hash = md5.ComputeHash(bytes);
                        for (var j = 0; j < 4; j++)
                        {
                            var key = ((long) (hash[3 + j*4] & 0xFF) << 24)
                                      | ((long) (hash[2 + j*4] & 0xFF) << 16)
                                      | ((long) (hash[1 + j*4] & 0xFF) << 8)
                                      | (uint) (hash[0 + j*4] & 0xFF);

                            _buckets[key] = server;
                        }
                    }
                }
            }
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