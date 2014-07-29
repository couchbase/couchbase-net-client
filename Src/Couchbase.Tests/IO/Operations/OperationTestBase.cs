using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Serializers;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    public abstract class OperationTestBase
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        protected readonly AutoByteConverter Converter = new AutoByteConverter();
        protected  ITypeSerializer Serializer;
        private static readonly string Address = ConfigurationManager.AppSettings["OperationTestAddress"];

        [TestFixtureSetUp]
        public virtual void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<EapConnection>(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        internal IVBucket GetVBucket()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.First();
            var vBucketServerMap = bucket.VBucketServerMap;

            var servers = vBucketServerMap.
                ServerList.
                Select(server => new Server(_ioStrategy, new Node(), new ClientConfiguration())).
                Cast<IServer>().
                ToList();

            var vBucketMap = vBucketServerMap.VBucketMap.First();
            var primary = vBucketMap[0];
            var replicas = new int[]{vBucketMap[1]};
            return new VBucket(servers, 0, primary, replicas);
        }

        internal IOStrategy IOStrategy { get { return _ioStrategy; } }

        [TestFixtureTearDown]
        public virtual void TestFixtureTearDown()
        {
            _connectionPool.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            Serializer = new TypeSerializer(Converter);
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