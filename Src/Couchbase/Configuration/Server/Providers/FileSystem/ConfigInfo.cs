using System;
using System.Collections.Generic;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;

namespace Couchbase.Configuration.Server.Providers.FileSystem
{
    [Obsolete]
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


        public void Authenticate(string username, string password)
        {
            throw new NotImplementedException();
        }


        public void LoadConfig()
        {
            throw new NotImplementedException();
        }
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
