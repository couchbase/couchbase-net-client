using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Services;
using Couchbase.Tests.Fakes;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    public abstract class OperationTestBase
    {
        private IConnectionPool _connectionPool;
        protected ITypeTranscoder Transcoder = new DefaultTranscoder();
        private static readonly string Address = ConfigurationManager.AppSettings["OperationTestAddress"];
        protected IPEndPoint EndPoint;
        protected static readonly uint OperationLifespanTimeout = 2500; //2.5sec

        [SetUp]
        public virtual void OneTimeSetUp()
        {
            EndPoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, EndPoint);
            IOService = new PooledIOService(_connectionPool);
            Transcoder = new DefaultTranscoder();
        }

        internal IVBucket GetVBucket()
        {
            var bucketConfig = ConfigUtil.ServerConfig.Buckets.First(x => x.Name=="default");
            var vBucketServerMap = bucketConfig.VBucketServerMap;

            var servers = new Dictionary<IPEndPoint, IServer>();
            foreach (var node in bucketConfig.GetNodes())
            {
                servers.Add(IPEndPointExtensions.GetEndPoint(node.Hostname + ":" + node.KeyValue),
                    new Server(IOService,
                        node,
                        new FakeTranscoder(),
                        ContextFactory.GetCouchbaseContext(bucketConfig)));
            }

            var vBucketMap = vBucketServerMap.VBucketMap.First();
            var primary = vBucketMap[0];
            var replicas = new []{vBucketMap[1]};
            return new VBucket(servers, 0, primary, replicas, bucketConfig.Rev, vBucketServerMap, "default");
        }

        internal IIOService IOService { get; private set; }

        [TearDown]
        public virtual void TearDown()
        {
            _connectionPool.Dispose();
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