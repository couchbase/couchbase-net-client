using System.Collections.Generic;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Services;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests
{
    public class ScramShaMechanismTests
    {
        private IIOService _ioService;
        private IConnectionPool _connectionPool;
        private readonly string _address = TestConfiguration.Settings.Hostname + ":11210";

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
            var authenticator = new ScramShaMechanism(_ioService, new DefaultTranscoder(), MechanismType.ScramSha1);
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
            var authenticator = new ScramShaMechanism(_ioService, new DefaultTranscoder(), MechanismType.ScramSha1);
            _ioService.SaslMechanism = authenticator;

            foreach (var connection in _ioService.ConnectionPool.Connections)
            {
                var isAuthenticated = authenticator.Authenticate(connection, "authenticated", "secret2");
                Assert.IsFalse(isAuthenticated);
            }
        }

        [Test]
        public void DecodeResponse_ConvertsServerResponse_ToDictionary()
        {
            var response =
                "r=SldM4VtjuyfZSJPHsUwgFaAFooUHKcg0l4HJdOkwmho=f740159ca94603c2,s=FOim+HVE8xUmn9Std27QgrqZiWzPLM6K1NiklQ3KunEugY1OSG/jlEzQ9XVdLkcSIqegM5O2gLF2mAQio+CXBA==,i=4096";

            var authenticator = new ScramShaMechanism(_ioService, new DefaultTranscoder(), MechanismType.ScramSha512);
            var actual = authenticator.DecodeResponse(response);
            var expected = new Dictionary<string, string>
            {
                {"r", "SldM4VtjuyfZSJPHsUwgFaAFooUHKcg0l4HJdOkwmho=f740159ca94603c2"},
                {"s", "FOim+HVE8xUmn9Std27QgrqZiWzPLM6K1NiklQ3KunEugY1OSG/jlEzQ9XVdLkcSIqegM5O2gLF2mAQio+CXBA=="},
                {"i", "4096"}
            };

            Assert.AreEqual(expected, actual);
        }

        /*
        User name: user
        Password:  pencil
        Client nonce: fyko+d2lbbFgONRv9qkxdawL
        Server nonce: 3rfcNHYJY1ZVvWVs7j
        Iteration count: 4096
        Password salt:  QSXCR+Q6sek8bf92

        xxxxxxxxxxxxxxxxxxxxxxx Test Vectors xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx

        Client First Message: n,,n=user,r=fyko+d2lbbFgONRv9qkxdawL
        Server First Message: r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096
        Client Final Message: c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=
        Server Final Message: v=rmF9pqV8S7suAoZWja4dJRkFsKQ=
        */

        [Test]
        public void Authenticate_Returns_ClientFinalMessage()
        {
            var mockedService = new Mock<IIOService>();
            mockedService.Setup(x => x.Execute(It.IsAny<SaslStart>(), It.IsAny<IConnection>()))
                .Returns(new OperationResult<string>()
                {
                    Status = ResponseStatus.AuthenticationContinue,
                    Message = "r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,s=QSXCR+Q6sek8bf92,i=4096"
                });


            mockedService.Setup(x => x.Execute(It.IsAny<SaslStep>(), It.IsAny<IConnection>()))
                .Returns(new OperationResult<string>()
                {
                    Status = ResponseStatus.AuthenticationContinue,
                    Message = "v=rmF9pqV8S7suAoZWja4dJRkFsKQ="
                });

            var mockedConnection = new Mock<IConnection>();
            var authenticator = new ScramShaMechanism(mockedService.Object, new DefaultTranscoder(), MechanismType.ScramSha1);
            authenticator.ClientNonce = "fyko+d2lbbFgONRv9qkxdawL";
            authenticator.Authenticate(mockedConnection.Object, "user", "pencil");

            var expected = "c=biws,r=fyko+d2lbbFgONRv9qkxdawL3rfcNHYJY1ZVvWVs7j,p=v0X8v3Bz2T0CJGbJQyF0X+HI4Ts=";
            Assert.AreEqual(expected, authenticator.ClientFinalMessage);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
