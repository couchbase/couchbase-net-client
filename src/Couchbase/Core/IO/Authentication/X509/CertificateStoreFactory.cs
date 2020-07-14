using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Core.IO.Authentication.X509
{
    /// <summary>
    /// A CertificateFactory that queries the X509Store for a certificate using the search criteria here:
    /// https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.x509certificates.x509store?view=netcore-3.1
    /// </summary>
    public class CertificateStoreFactory : ICertificateFactory
    {
        private readonly CertificateStoreSearchCriteria _searchCriteria;

        public CertificateStoreFactory(CertificateStoreSearchCriteria searchCriteria)
        {
            _searchCriteria = searchCriteria;
        }

        public X509Certificate2Collection GetCertificates()
        {
            var store = new X509Store(_searchCriteria.StoreName, _searchCriteria.StoreLocation);
            store.Open(OpenFlags.ReadOnly);
            return store.Certificates.Find(_searchCriteria.X509FindType, _searchCriteria.FindValue, false);
        }
    }
}

#region [ License information          ]
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2020 Couchbase, Inc.
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
