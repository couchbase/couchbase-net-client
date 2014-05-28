using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Strategies;
using Couchbase.IO.Strategies.Async;
using Couchbase.IO.Strategies.Awaitable;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations.Authentication
{
    [TestFixture]
    public class SaslAuthenticateTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private const string Address = "127.0.0.1:11210";

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = Couchbase.Core.Server.GetEndPoint(Address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<EapConnection>(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_SaslAuthenticate_Returns_AuthFailure_With_InvalidCredentials()
        {
            var operation = new SaslStart("PLAIN",  GetAuthData("foo", "bar"));
            var response = _ioStrategy.Execute(operation);

            Assert.AreEqual("Auth failure", response.Message);
            Assert.AreEqual(ResponseStatus.AuthenticationError, response.Status);
            Assert.IsFalse(response.Success);
        }


        [Test]
        public void Test_SaslAuthenticate_Returns_Succuss_With_ValidCredentials()
        {
            var operation = new SaslStart("PLAIN",  GetAuthData("authenticated", "secret"));
            var response = _ioStrategy.Execute(operation);

            Assert.AreEqual("Authenticated", response.Value);
            Assert.AreEqual(ResponseStatus.Success, response.Status);             
            Assert.IsTrue(response.Success);
        }

        [Test]
        public void When_CRAM_MD5_Used_SaslStart_Returns_AuthenticationContinue()
        {
            var operation = new SaslStart("CRAM-MD5", null);
            var response = _ioStrategy.Execute(operation);

            Assert.IsNotNullOrEmpty(response.Message);
            Assert.AreEqual(ResponseStatus.AuthenticationContinue, response.Status);
            Assert.IsFalse(response.Success);
        }

        static string GetAuthData(string userName, string passWord)
        {
            const string empty = "\0";
            var sb = new StringBuilder();
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(userName);
            sb.Append(empty);
            sb.Append(passWord);
            return sb.ToString();
        }

        [Test]
        public void Test_Configure_Client_With_CramMd5()
        {
            var configuration = new ClientConfiguration
            {
                SaslMechanism = SaslMechanismType.CramMd5
            };

            CouchbaseCluster.Initialize(configuration);
            var cluster = CouchbaseCluster.Get();
            using (var bucket = cluster.OpenBucket())
            {
                
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