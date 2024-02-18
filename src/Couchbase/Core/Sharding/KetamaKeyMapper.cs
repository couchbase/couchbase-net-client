using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
     /// <summary>
    /// Provides a means of consistent hashing for keys used by Memcached Buckets.
    /// </summary>
    internal sealed class KetamaKeyMapper : IKeyMapper
    {
        private readonly SortedDictionary<long, KetamaNode> _hashes = [];
        private long[] _sortedKeys;

        public KetamaKeyMapper(IEnumerable<HostEndpointWithPort> servers)
        {
            Initialize(servers.Select(static p => new KetamaNode(p)));
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

            return _hashes[_sortedKeys[index]];
        }

        /// <summary>
        /// Not Supported: This overload is only supported by Couchbase buckets.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="notMyVBucket"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key, bool notMyVBucket)
        {
            ThrowHelper.ThrowNotSupportedException("This overload is only supported by Couchbase buckets.");
            return null!; // unreachable
        }

        /// <summary>
        /// Finds the index of a node for a given key.
        /// </summary>
        /// <param name="key">The Key that the index belongs to.</param>
        /// <returns>The index of key - which is the location of the node that the key maps to.</returns>
        public int FindIndex(long key)
        {
            var index = Array.BinarySearch(_sortedKeys, key);
            if (index < 0)
            {
                index = ~index;
                if (index == 0)
                {
                    index = _hashes.Count - 1;
                }
                else if (index >= _hashes.Count)
                {
                    index = 0;
                }
            }
            if (index < 0 || index >= _hashes.Count)
            {
                ThrowHelper.ThrowInvalidOperationException("Invalid index");
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
#if !NET6_0_OR_GREATER
            var bytes = Encoding.UTF8.GetBytes(key);

            Span<byte> hash;
            using (var md5 = MD5.Create())
            {
                hash = md5.ComputeHash(bytes).AsSpan();
            }
#else
            Span<byte> bytes = stackalloc byte[OperationHeader.MaxKeyLength];
            var byteCount = Encoding.UTF8.GetBytes(key, bytes);
            bytes = bytes.Slice(0, byteCount);

            Span<byte> hash = stackalloc byte[16]; // MD5 is a 16 byte hash
            if (!MD5.TryHashData(bytes, hash, out _))
            {
                Debug.Fail("Insufficient buffer");
            }
#endif

            return BinaryPrimitives.ReadUInt32LittleEndian(hash);
        }

        /// <summary>
        /// Initializes the mapping of hashes to nodes.
        /// </summary>
        [MemberNotNull(nameof(_sortedKeys))]
        public void Initialize(IEnumerable<KetamaNode> nodes)
        {
#if !NET6_0_OR_GREATER
            using var md5 = MD5.Create();
#else
            Span<byte> hash = stackalloc byte[16]; // MD5 is a 16 byte hash
#endif

            foreach (var node in nodes)
            {
                for (var rep = 0; rep < 40; rep++)
                {
                    var bytes = Encoding.UTF8.GetBytes($"{node.Server}-{rep}");

#if !NET6_0_OR_GREATER
                    var hash = md5.ComputeHash(bytes).AsSpan();
#else
                    if (!MD5.TryHashData(bytes, hash, out _))
                    {
                        Debug.Fail("Insufficient buffer");
                    }
#endif
                    for (var j = 0; j < 4; j++)
                    {
                        var key = BinaryPrimitives.ReadUInt32LittleEndian(hash.Slice(j * 4));
                        _hashes[key] = node;
                    }
                }
            }

            _sortedKeys = [.. _hashes.Keys];
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
