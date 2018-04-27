using System;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Configuration.Client;

namespace Couchbase.Authentication.X509
{
    /// <summary>
    /// Factory class for creating Func{X509Certificate2Collection} instances that can be assigned to the <see cref="CertificateFactory"></see> property.
    /// </summary>
    public static class CertificateFactory
    {
        /// <summary>
        /// Creates an <see cref="Func{X509Certificate2Collection}"/> given a path and password to a .pfx certificate.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Func<X509Certificate2Collection> GetCertificatesByPathAndPassword(PathAndPasswordOptions options)
        {
            return () =>
            {
                if(options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                return new X509Certificate2Collection(new X509Certificate2(options.Path, options.Password));
            };
        }

        /// <summary>
        /// Creates an <see cref="Func{X509Certificate2Collection}"/> given <see cref="CertificateStoreOptions"/> to find a .pfx
        /// certificate in the Windows Cert Store.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public static Func<X509Certificate2Collection> GetCertificatesFromStore(CertificateStoreOptions options)
        {
            return () =>
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                var store = new X509Store(options.StoreName, options.StoreLocation);
                store.Open(OpenFlags.ReadOnly);
                return store.Certificates.Find(options.X509FindType, options.FindValue, false);
            };
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
