using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations.Authentication
{
    [TestFixture]
    public class SaslAuthenticateTests
    {
        [TestFixture]
        public class SaslListMechanismsOperationTests
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

                _ioStrategy = new AwaitableIOStrategy(_connectionPool, null);
            }

            [Test]
            public async void Test_SaslAuthenticate_Returns_AuthFailure_With_InvalidCredentials()
            {
                var operation = new SaslAuthenticate("PLAIN", "foo", "bar");
                var response = await _ioStrategy.ExecuteAsync(operation);

                Console.WriteLine(response.Value);
                Assert.AreEqual("Auth failure", response.Value);
                Assert.AreEqual(ResponseStatus.AuthenticationError, response.Status);
                Assert.IsFalse(response.Success);
            }


            [Test]
            public async void Test_SaslAuthenticate_Returns_Succuss_With_ValidCredentials()
            {
                var operation = new SaslAuthenticate("PLAIN", "authenticated", "secret");
                var response = await _ioStrategy.ExecuteAsync(operation);

                Console.WriteLine(response.Value);
                Assert.AreEqual("Authenticated", response.Value);
                Assert.AreEqual(ResponseStatus.Success, response.Status);             
                Assert.IsTrue(response.Success);
            }

            [TestFixtureTearDown]
            public void TearDown()
            {
                _connectionPool.Dispose();
            }
        }
    }
}
