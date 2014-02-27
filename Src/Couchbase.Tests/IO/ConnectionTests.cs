using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO;
using NUnit.Framework;

namespace Couchbase.Tests.IO
{
    [TestFixture]
    public class ConnectionTests
    {
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new DefaultConnectionPool(connectionPoolConfig, ipEndpoint);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Is_Not_Null()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsNotNull(connection.Handle);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Has_Identity()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsNotNull(connection.Identity);
            Assert.IsTrue(connection.Identity != Guid.Empty);
        }

        [Test]
        public void When_Connection_Acquired_Handle_Is_Connected()
        {
            var connection = _connectionPool.Acquire();
            Assert.IsTrue(connection.Handle.Connected);
        }

        [Test]
        public void When_Dispose_Called_Handle_Is_Not_Connected()
        {
            var connection = _connectionPool.Acquire();
            connection.Dispose();
            Assert.IsFalse(connection.Handle.Connected);
        }

        [TearDown]
        public void TestFixtureTearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
