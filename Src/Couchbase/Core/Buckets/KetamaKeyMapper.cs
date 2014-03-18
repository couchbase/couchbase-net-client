using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.Core.Buckets
{
    internal class KetamaKeyMapper : IKeyMapper
    {
        private readonly List<IServer> _servers;
        private readonly int _totalWeight;
        private readonly SortedDictionary<long, IServer> _buckets = new SortedDictionary<long, IServer>();

        public KetamaKeyMapper(List<IServer> servers) 
            : this(servers, MD5.Create())
        {
        }

        public KetamaKeyMapper(List<IServer> servers, HashAlgorithm algorithm)
        {
            _servers = servers;
            _totalWeight = _servers.Count;
            HashAlgorithm = algorithm;
            Initialize();
        }
 
        public IMappedNode MapKey(string key)
        {
            var hash = GetHash(key);
            var index = FindIndex(hash);
            var server = _buckets[_buckets.Keys.ToList()[index]];

            return new KetamaNode(server);
        }

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

        public long GetHash(string key)
        {
            var bytes = Encoding.UTF8.GetBytes(key);
            var hash = HashAlgorithm.ComputeHash(bytes);
            var result = ((long) (hash[3] & 0xFF) << 24)
                | ((long)(hash[2] & 0xFF) << 16)
                | ((long)(hash[1] & 0xFF) << 8)
                | hash[0] & 0xFF;
            return result;
        }

        public void Initialize()
        {
            foreach (var server in _servers)
            {
                var weight = 1;//may change this later
                var factor = Math.Floor(40*_servers.Count()*weight/(double) _totalWeight);

                for (long n = 0; n < factor; n++)
                {
                    var bytes = Encoding.UTF8.GetBytes(server.EndPoint + "-" + n);
                    var hash = HashAlgorithm.ComputeHash(bytes);
                    for (var j = 0; j < 4; j++)
                    {
                        var key = ((long) (hash[3 + j*4] & 0xFF) << 24)
                                  | ((long) (hash[2 + j*4] & 0xFF) << 16)
                                  | ((long) (hash[1 + j*4] & 0xFF) << 8)
                                  | hash[0 + j*4] & 0xFF;

                        _buckets[key] = server;
                    }
                }
            }
        }

        public HashAlgorithm HashAlgorithm { get; set; }
    }
}
