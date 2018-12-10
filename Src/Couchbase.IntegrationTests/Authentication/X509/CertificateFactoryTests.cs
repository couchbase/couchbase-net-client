using System.Security.Cryptography.X509Certificates;
using Couchbase.Authentication.X509;
using Couchbase.IntegrationTests.Utils;
using NUnit.Framework;

namespace Couchbase.IntegrationTests.Authentication.X509
{
    [TestFixture]
    [Ignore("Build server needs permission set.")]
    public class CertificateFactoryTests
    {
        [OneTimeSetUp]
        public void Setup()
        {
            TestConfiguration.IgnoreIfMock();
        }

        [Test]
        public void Test_GetCertificateByPathAndPassword()
        {
            var factory = CertificateFactory.GetCertificatesByPathAndPassword(
                new PathAndPasswordOptions
                {
                    Path = TestContext.CurrentContext.TestDirectory + "\\client.pfx",
                    Password = "password"
                });
            var cert = factory();
            Assert.IsNotNull(cert);
        }

        [Test]
        public void Test_GetCertificatesFromStore()
        {
            var factory = CertificateFactory.GetCertificatesFromStore(
                new CertificateStoreOptions
                {
                    StoreLocation = StoreLocation.LocalMachine,
                    StoreName = StoreName.Root,
                    X509FindType = X509FindType.FindByIssuerName,
                    FindValue = "MyCompanyIntermediateCA"
                });
            var cert = factory();
            Assert.IsNotNull(cert);
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
