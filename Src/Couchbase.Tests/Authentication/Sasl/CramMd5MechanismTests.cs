using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class CramMd5MechanismTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<EapConnection>(connectionPoolConfig, ipEndpoint);
            _connectionPool.Initialize();
            _ioStrategy = new DefaultIOStrategy(_connectionPool, null);
        }


        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy);
            _ioStrategy.ConnectionPool.Initialize();
            _ioStrategy.SaslMechanism = authenticator;

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "secret");
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_InValid_Credentials_Provided_Authenticate_Returns_False()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy);
            _ioStrategy.ConnectionPool.Initialize();
            _ioStrategy.SaslMechanism = authenticator;

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "wrongpass");
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True2()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "authenticated", "secret");
            _ioStrategy.ConnectionPool.Initialize();
            _ioStrategy.SaslMechanism = authenticator;

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_InValid_Credentials_Provided_Authenticate_Returns_False2()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "authenticated", "wrongpass");
            _ioStrategy.ConnectionPool.Initialize();
            _ioStrategy.SaslMechanism = authenticator;

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void When_Bucket_Has_No_Password_And_Password_Is_Null_Authenticate_Succeeds()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "default", null);
            _ioStrategy.ConnectionPool.Initialize();
            _ioStrategy.SaslMechanism = authenticator;

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_Bucket_Has_No_Password_Authenticate_Succeeds()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "default", string.Empty);
            _ioStrategy.ConnectionPool.Initialize();

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        
        [Test]
        public void Test_ComputeResponse()
        {
            var authenticator = new CramMd5Mechanism(_ioStrategy, "protected", "secret");
            const string challenge = "6382f3e79a804548"; //"15cedeaaf8b06c34";
            const string expected = "protected 3ca7b9f1b81bc7f6c2c9e5f48af3311d"; //"protected 06f8b68edb01c7b453f50429d4bfb195";

            var actual = authenticator.ComputeResponse(challenge);
            Assert.AreEqual(expected, actual);
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
