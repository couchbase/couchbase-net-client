using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;
using System;

namespace Couchbase.Tests.IO.Strategies.Awaitable
{
    [TestFixture]
    public class AwaitableIOStrategyTests
    {
        private AwaitableIOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Couchbase.Core.Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new AwaitableIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_ExecuteAsnyc()
        {
            var operation = new ConfigOperation();
            var task = _ioStrategy.ExecuteAsync(operation);

            try
            {
                task.Wait();
            }
            catch (AggregateException ae)
            {
                ae.Flatten().Handle(e =>
                {
                    Console.WriteLine(e);
                    return true;
                });
            }

            var result = task.Result;
            Assert.IsTrue(result.Success);
            Console.WriteLine(result.Value);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}