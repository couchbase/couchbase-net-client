using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Cryptography;

namespace Couchbase.Configuration
{
    internal class ConfigInfo : IConfigInfo
    {
        private readonly List<IServer> _servers = new List<IServer>();
        private readonly Dictionary<BucketConfig, Dictionary<int, IVBucket>> _buckets = new Dictionary<BucketConfig, Dictionary<int, IVBucket>>();

        public ConfigInfo(IServerConfig serverConfig, ClientConfiguration clientConfig)
        {
            ServerConfig = serverConfig;
            ClientConfig = clientConfig;
            CreationTime = DateTime.Now;
            Initialize();
        }

        void Initialize()
        {
            var nodes = ServerConfig.Pools.Nodes;
            foreach (var node in nodes)
            {
                //_servers.Add(new Core.Server(node.Hostname));
            }

            var buckets = ServerConfig.Buckets;
            foreach (var bucket in buckets)
            {
                var vBuckets = new Dictionary<int, IVBucket>();
                var serverMap = bucket.VBucketServerMap;

                if (bucket.BucketType != "memcached")
                {
                    for (var i = 0; i < bucket.VBucketServerMap.VBucketMap.Length; i++)
                    {
                        if (serverMap == null) continue;
                        var primary = serverMap.VBucketMap[i][0];
                        var replica = serverMap.VBucketMap[i][1];
                        vBuckets.Add(i, new VBucket(_servers, i, primary, replica));
                    }
                }
                _buckets.Add(bucket, vBuckets);
            }
        }

        public DateTime CreationTime { get; private set; }

       /* public IKeyMapper<T> GetKeyMapper<T>(string bucketName)
        {
            var bucket = _buckets.Keys.FirstOrDefault(x => x.Name == bucketName);
            if (bucket == null)
            {
                throw new BucketNotFoundException();
            }
                
            IKeyMapper keyMapper = null;
            switch (bucket.BucketType)
            {
                case "memcached":
                    //create a ketamamapper
                    break;
                case "membase":
                   // keyMapper = new KeyMapper(new Crc32(), _buckets[bucket]);
                    break;
                default:
                    throw new InvalidBucketTypeException();
            }
            return keyMapper;
        }*/

        public IServerConfig ServerConfig { get; private set; }

        public ClientConfiguration ClientConfig { get; private set; }


        public IBucketConfig BucketConfig
        {
            get { throw new NotImplementedException(); }
        }

        public string BucketName
        {
            get { throw new NotImplementedException(); }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public Core.Buckets.BucketTypeEnum BucketType
        {
            get { throw new NotImplementedException(); }
        }


        public Core.Buckets.NodeLocatorEnum NodeLocator
        {
            get { throw new NotImplementedException(); }
        }


        public IKeyMapper GetKeyMapper(string bucketName)
        {
            throw new NotImplementedException();
        }


        IKeyMapper IConfigInfo.GetKeyMapper(string bucketName)
        {
            throw new NotImplementedException();
        }


        public IServer GetServer()
        {
            throw new NotImplementedException();
        }
    }
}
