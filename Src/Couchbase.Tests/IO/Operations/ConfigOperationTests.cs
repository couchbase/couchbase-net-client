using System;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigOperationTests
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

        [Test]
        public void Test_GetConfig()
        {
            var response = _ioStrategy.Execute(new ConfigOperation());
            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Console.WriteLine(response.Value.ToString());
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
