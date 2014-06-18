using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.EAP;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Strategies.EAP
{
   // [TestFixture]
    public class EapIoStrategyTests
    {
        private EapioStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);       
            _ioStrategy = new EapioStrategy(_connectionPool);
        }

       // [Test]
        public void TestGet()
        {
            for (int i = 0; i < 2; i++)
            {
                var operation = new ConfigOperation(new ManualByteConverter());
                var result = _ioStrategy.Execute(operation);
                Assert.IsTrue(result.Success);
                Assert.IsNotNull(result.Value);
                Console.WriteLine(result.Value.ToString());
            }
        }


        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
