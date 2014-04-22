using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class PlainTextMechanismTests
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
            _connectionPool.Initialize();
            _ioStrategy = new SocketAsyncStrategy(_connectionPool, null);
        }

        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            var isAuthenticated = authenticator.Authenticate("authenticated", "secret");
            Assert.IsTrue(isAuthenticated);
        }

        [Test]
        public void When_Valid_Invalid_Credentials_Provided_Authenticate_Returns_False()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            var isAuthenticated = authenticator.Authenticate("authenticated", "badpass");
            Assert.IsFalse(isAuthenticated);
        }

        [Test]
        public void When_Non_Sasl_Bucket_And_Empty_Password_Authenticate_Returns_true()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            var isAuthenticated = authenticator.Authenticate("default", "");
            Assert.IsTrue(isAuthenticated);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
