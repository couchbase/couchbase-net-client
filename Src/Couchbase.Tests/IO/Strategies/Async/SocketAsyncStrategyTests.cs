using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Strategies.Async
{
    [TestFixture]
    public class SocketAsyncStrategyTests
    {
        private SocketAsyncStrategy _ioStrategy;
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
        public void Test_Execute()
        {
            var operation = new ConfigOperation();
            var result = _ioStrategy.Execute(operation);
            var result1 = _ioStrategy.Execute(operation);
            Assert.IsNotNull(result.Value);
            Assert.IsNotNull(result1.Value);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}