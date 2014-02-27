using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Couchbase.Cryptography;

namespace Couchbase.Core
{
    internal class KeyMapper : IKeyMapper
    {
        private const int Mask = 1023;
        private readonly Dictionary<int, IVBucket> _vBuckets;
 
        public KeyMapper(HashAlgorithm algorithm, Dictionary<int, IVBucket> vBuckets)
        {
            HashAlgorithm = algorithm;
            _vBuckets = vBuckets;
        }

        public KeyMapper(Dictionary<int, IVBucket> vBuckets) 
            : this(new Crc32(), vBuckets)
        {
        }

        public IVBucket MapKey(string key)
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
    }
}
