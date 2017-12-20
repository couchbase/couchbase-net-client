using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Authentication.X509;
using Couchbase.Configuration.Client;
using Couchbase.IntegrationTests.Utils;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Utils;
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
