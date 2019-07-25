using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Utils;

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
        private readonly Couchbase.Configuration _configuration;

        public KetamaKeyMapper(BucketConfig config, Couchbase.Configuration configuration)
        {
            _configuration = configuration;
            _servers = GetIpEndPoints(config);
            _totalWeight = _servers.Count;
            Initialize();
        }

        private IList<IPEndPoint> GetIpEndPoints(BucketConfig config)
        {
            var ipEndPoints = new List<IPEndPoint>();
            foreach (var node in config.GetNodes())
                if (node.IsDataNode)
                    ipEndPoints.Add(node.GetIpEndPoint());

            return ipEndPoints;
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
        /// <param name="revision"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key, uint revision)
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

        public uint Rev { get; set; }
    }
}
