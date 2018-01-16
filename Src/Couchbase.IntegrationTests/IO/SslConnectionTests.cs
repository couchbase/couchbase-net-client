using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.X509;
using Couchbase.Configuration.Client;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Utils;
using Moq;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.IO
{
    [TestFixture]
    public class SslConnectionTests
    {
        [Test]
        public void Test_Authenticate_With_Ssl()
        {
            var endpoint = IPEndPointExtensions.GetEndPoint(TestConfiguration.Settings.Hostname, 11207);
            var bootstrapUri = new Uri($@"https://{endpoint.Address}:8091/pools");
            var connFactory = DefaultConnectionFactory.GetGeneric<SslConnection>();
            var poolFactory = ConnectionPoolFactory.GetFactory();

            ClientConfiguration.IgnoreRemoteCertificateNameMismatch = true;
            var poolConfig = new PoolConfiguration {UseSsl = true, Uri = bootstrapUri  };

            var pool = poolFactory(poolConfig, endpoint);
            var conn = connFactory((IConnectionPool<SslConnection>) pool, new DefaultConverter(), new BufferAllocator(1024 * 16, 1024 * 16));

            Assert.IsTrue(conn.IsConnected);
        }

        [Test]
        [TestCase(false, false, false)]
        [TestCase(false, false, true)]
        [TestCase(false, true, true)]
        [TestCase(true, false, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        public void Test_Authenticate(bool isAuthenticated, bool isEncrypted, bool isSigned)
        {
            var endpoint = IPEndPointExtensions.GetEndPoint(TestConfiguration.Settings.Hostname, 11207);
            var bootstrapUri = new Uri($@"https://{endpoint.Address}:8091/pools");
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endpoint);

            var poolConfig = new PoolConfiguration { UseSsl = true, Uri = bootstrapUri };
            var sslStream = new Mock<SslStream>(new MemoryStream(), true, new RemoteCertificateValidationCallback(ServerCertificateValidationCallback));
            sslStream.Setup(x => x.IsAuthenticated).Returns(isAuthenticated);
            sslStream.Setup(x => x.IsEncrypted).Returns(isEncrypted);
            sslStream.Setup(x => x.IsSigned).Returns(isSigned);

            var connPool = new Mock<IConnectionPool<IConnection>>();
            connPool.Setup(x => x.Configuration).Returns(poolConfig);
            var conn = new SslConnection(connPool.Object, socket, sslStream.Object, new DefaultConverter(),
                new BufferAllocator(1024, 1024));

            if (isAuthenticated && isEncrypted && isSigned)
            {
                Assert.DoesNotThrow(conn.Authenticate);
            }
            else
            {
                Assert.Throws<AuthenticationException>(conn.Authenticate);
            }
        }

        private static bool ServerCertificateValidationCallback(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        [Test]
        [Ignore("Depends on a X509 cert being generated, configured on the server and installed. See: https://developer.couchbase.com/documentation/server/current/security/security-x509certsintro.html")]
        public void Test_KV_Certificate_Authentication()
        {
            var config = TestConfiguration.GetConfiguration("ssl");
            config.UseSsl = true;

            ClientConfiguration.IgnoreRemoteCertificateNameMismatch = true;//ignore for now
            config.BucketConfigs["default"].EnableCertificateAuthentication = true;
            config.BucketConfigs["default"].CertificateFactory = CertificateFactory.GetCertificatesByPathAndPassword(
                new PathAndPasswordOptions
                {
                    Path = TestContext.CurrentContext.TestDirectory + "\\client.pfx",
                    Password = "password"
                });
            var cluster = new Cluster(config);

            var bucket = cluster.OpenBucket();
            var result = bucket.Upsert("mykey", "myvalue");
            Assert.AreEqual(ResponseStatus.Success, result.Status);
            Assert.IsNotNull(bucket);
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
