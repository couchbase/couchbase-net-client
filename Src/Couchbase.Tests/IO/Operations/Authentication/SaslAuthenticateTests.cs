using System.Configuration;
using System.Text;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Strategies;
using Couchbase.Utils;
using NUnit.Framework;

namespace Couchbase.Tests.IO.Operations.Authentication
{
    [TestFixture]
    public class SaslAuthenticateTests
    {
        private IOStrategy _ioStrategy;
        private IConnectionPool _connectionPool;
        private readonly string _address = ConfigurationManager.AppSettings["OperationTestAddress"];
        private const uint OperationLifespan = 2500; //ms

        [TestFixtureSetUp]
        public void TestFixtureSetUp()
        {
            var ipEndpoint = UriExtensions.GetEndPoint(_address);
            var connectionPoolConfig = new PoolConfiguration();
            _connectionPool = new ConnectionPool<Connection>(connectionPoolConfig, ipEndpoint);
            _ioStrategy = new DefaultIOStrategy(_connectionPool);
        }

        [Test]
        public void Test_SaslAuthenticate_Returns_AuthFailure_With_InvalidCredentials()
        {
            var operation = new SaslStart("PLAIN", GetAuthData("foo", "bar"), new DefaultTranscoder(), OperationLifespan);
            var response = _ioStrategy.Execute(operation);

            Assert.AreEqual("Auth failure", response.Message);
            Assert.AreEqual(ResponseStatus.AuthenticationError, response.Status);
            Assert.IsFalse(response.Success);
        }

        [Test]
        public void Test_SaslAuthenticate_Returns_Succuss_With_ValidCredentials()
        {
            var operation = new SaslStart("PLAIN", GetAuthData("authenticated", "secret"), new DefaultTranscoder(), OperationLifespan);
            var response = _ioStrategy.Execute(operation);

            Assert.AreEqual("Authenticated", response.Value);
            Assert.AreEqual(ResponseStatus.Success, response.Status);
            Assert.IsTrue(response.Success);
        }

        [Test]
        public void When_CRAM_MD5_Used_SaslStart_Returns_AuthenticationContinue()
        {
            var operation = new SaslStart("CRAM-MD5", (VBucket)null, new DefaultTranscoder(), OperationLifespan);
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