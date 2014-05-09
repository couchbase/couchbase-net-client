using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    public abstract class OperationTestBase
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Couchbase.Core.Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new SocketAsyncStrategy(_connectionPool);
        }

        internal IVBucket GetVBucket()
        {
            var bucket = ConfigUtil.ServerConfig.Buckets.First();
            var vBucketServerMap = bucket.VBucketServerMap;

            var servers = vBucketServerMap.
                ServerList.
                Select(server => new Server(_ioStrategy, new Node())).
                Cast<IServer>().
                ToList();

            var vBucketMap = vBucketServerMap.VBucketMap.First();
            var primary = vBucketMap[0];
            var replica = vBucketMap[1];
            return new VBucket(servers, 0, primary, replica);
        }

        internal IOStrategy IOStrategy { get { return _ioStrategy; } }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
