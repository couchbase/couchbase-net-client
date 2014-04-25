using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Async;
using NUnit.Framework;
using System;

namespace Couchbase.Tests.IO.Operations
{
    [TestFixture]
    public class ConfigOperationTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Couchbase.Core.Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration
            {
                MinSize = 1,
                MaxSize = 1
            };
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

        [Test]
        public void Test_GetConfig_Non_Default_Bucket()
        {
            var saslMechanism = new PlainTextMechanism(_ioStrategy, "authenticated", "secret");
            _ioStrategy = new SocketAsyncStrategy(_connectionPool, saslMechanism);

            var response = _ioStrategy.Execute(new ConfigOperation());

            Assert.IsTrue(response.Success);
            Assert.IsNotNull(response.Value);
            Assert.AreEqual("authenticated", response.Value.Name);
            Console.WriteLine(response.Value.ToString());
        }

        [TearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}