using System.Configuration;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Strategies;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.Authentication.Sasl
{
    [TestFixture]
    public class PlainTextMechanismTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];

        [SetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _connectionPool.Initialize();
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void When_Valid_Credentials_Provided_Authenticate_Returns_True()
        {
            var authenticator = new PlainTextMechanism(_ioStrategy, new DefaultTranscoder());
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
            var authenticator = new PlainTextMechanism(_ioStrategy, new DefaultTranscoder());
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
            var authenticator = new PlainTextMechanism(_ioStrategy, new DefaultTranscoder());
            _ioStrategy.ConnectionPool.Initialize();

            foreach (var connection in _ioStrategy.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "default", "");
                Assert.IsTrue(isAuthenticated);
            }
        }

        [TearDown]
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