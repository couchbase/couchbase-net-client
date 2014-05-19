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
            _ioStrategy = new SocketAsyncStrategy(_connectionPool);
        }

        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            _ioStrategy.ConnectionPool.Initialize();

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "secret");
                Assert.IsTrue(isAuthenticated);
            }
        }

        [Test]
        public void When_Valid_Invalid_Credentials_Provided_Authenticate_Returns_False()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            _ioStrategy.ConnectionPool.Initialize();

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "badpass");
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void When_Non_Sasl_Bucket_And_Empty_Password_Authenticate_Returns_true()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy);
            _ioStrategy.ConnectionPool.Initialize();

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "default", "");
                Assert.IsTrue(isAuthenticated);
            }
        }

        [TestFixtureTearDown]
        public void TearDown()
        {
            _connectionPool.Dispose();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion