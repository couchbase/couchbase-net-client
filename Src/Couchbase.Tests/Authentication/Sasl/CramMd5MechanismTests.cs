using System.Configuration;
using System.IO;
using System.Net.Sockets;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Services;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class CramMd5MechanismTests
    {
        private IIOService _ioService;
        private IConnectionPool _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _connectionPool.Initialize();
            _ioService = new PooledIOService(_connectionPool, null);
        }


        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True()
        {
            var authenticator = new CramMd5Mechanism(_ioService, new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "secret");
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_InValid_Credentials_Provided_Authenticate_Returns_False()
        {
            var authenticator = new CramMd5Mechanism(_ioService, new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "wrongpass");
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True2()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "authenticated", "secret", new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_InValid_Credentials_Provided_Authenticate_Returns_False2()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "authenticated", "wrongpass", new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void When_Bucket_Has_No_Password_And_Password_Is_Null_Authenticate_Succeeds()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "default", null, new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_Bucket_Has_No_Password_Authenticate_Succeeds()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "default", string.Empty, new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection);
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void Test_ComputeResponse()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "protected", "secret", new DefaultTranscoder());
            const string challenge = "6382f3e79a804548"; //"15cedeaaf8b06c34";
            const string expected = "protected 3ca7b9f1b81bc7f6c2c9e5f48af3311d"; //"protected 06f8b68edb01c7b453f50429d4bfb195";

            var actual = authenticator.ComputeResponse(challenge);
            Assert.AreEqual(expected, actual);
        }

        [Test]
        [ExpectedException(typeof(SocketException))]
        public void When_IOException_Occurs_Authenticate_Throws_Exception()
        {
            var authenticator = new CramMd5Mechanism(_ioService, "default", string.Empty, new DefaultTranscoder());
            _ioService.ConnectionPool.Initialize();

            var connection = _ioService.ConnectionPool.Acquire();
            connection.Socket.Disconnect(false);

            authenticator.Authenticate(connection);
        }

        [TearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}
