using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
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
        private const int Mask = 1023;
        private readonly Dictionary<int, IVBucket> _vBuckets;
        private readonly VBucketServerMap _vBucketServerMap;
        private readonly List<IServer> _servers;

        public VBucketKeyMapper(List<IServer> servers, VBucketServerMap vBucketServerMap) 
            : this(new Crc32(), servers, vBucketServerMap)
        {
        }

        public VBucketKeyMapper(HashAlgorithm algorithm, List<IServer> servers, VBucketServerMap vBucketServerMap)
        {
            HashAlgorithm = algorithm;
            _servers = servers;
            _vBucketServerMap = vBucketServerMap;
            _vBuckets = CreateVBuckets();
        }
 
        public VBucketKeyMapper(HashAlgorithm algorithm, Dictionary<int, IVBucket> vBuckets)
        {
            HashAlgorithm = algorithm;
            _vBuckets = vBuckets;
        }

        public VBucketKeyMapper(Dictionary<int, IVBucket> vBuckets) 
            : this(new Crc32(), vBuckets)
        {
        }

        /// <summary>
        /// Maps a given Key to it's node in a Couchbase Cluster.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public IMappedNode MapKey(string key)
        {
            var index = GetIndex(key);
            return _vBuckets[index];
        }

        /// <summary>
        /// The alogrithm for hashing the keys. Couchbase Buckets use CRC32.
        /// </summary>
        public HashAlgorithm HashAlgorithm { get; set; }

        public int GetIndex(string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var hashedKeyBytes = HashAlgorithm.ComputeHash(keyBytes);
            var hash = BitConverter.ToUInt32(hashedKeyBytes, 0);
            return (int)hash & Mask;
        }

        /// <summary>
        /// Creates a mapping of VBuckets to nodes.
        /// </summary>
        /// <returns>A mapping of indexes and Vbuckets.</returns>
        Dictionary<int, IVBucket> CreateVBuckets()
        {
            var vBuckets = new Dictionary<int, IVBucket>();
            var vBucketForwardMap = _vBucketServerMap.VBucketMapForward;
            var vBucketMap = _vBucketServerMap.VBucketMap;
            Log.Info(m=>m("Creating VBuckets {0} and FMaps {1}", vBucketMap.Length, vBucketForwardMap.Length));
            for (var i = 0; i < vBucketMap.Length; i++)
            {
                var primary = vBucketMap[i][0];
                var replica = vBucketMap[i][1];
                vBuckets.Add(i, new VBucket(_servers, i, primary, replica));
            }
            return vBuckets;
        }
    }
}
