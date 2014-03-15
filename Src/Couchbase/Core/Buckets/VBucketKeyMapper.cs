using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Cryptography;

namespace Couchbase.Core.Buckets
{
    internal class VBucketKeyMapper : IKeyMapper
    {
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
            _servers = servers;
            _vBucketServerMap = vBucketServerMap;
            HashAlgorithm = algorithm;
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

        public IMappedNode MapKey(string key)
        {
            var index = GetIndex(key);
            return _vBuckets[index];
        }

        public HashAlgorithm HashAlgorithm { get; set; }

        public int GetIndex(string key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            var hashedKeyBytes = HashAlgorithm.ComputeHash(keyBytes);
            var hash = BitConverter.ToUInt32(hashedKeyBytes, 0);
            return (int)hash & Mask;
        }

        Dictionary<int, IVBucket> CreateVBuckets()
        {
            var vBuckets = new Dictionary<int, IVBucket>();
            var vBucketMap = _vBucketServerMap.VBucketMap;
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
