using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Authentication.X509
{
    /// <summary>
    /// An options class for locating a certification store and returning a <see cref="X509Certificate2Collection"/>.
    /// </summary>
    public class CertificateStoreOptions
    {
        /// <summary>
        /// Specifies the name of the X.509 certificate store to open.
        /// </summary>
        public StoreName StoreName { get; set; }

        /// <summary>
        /// Specifies the location of the X.509 certificate store.
        /// </summary>
        public StoreLocation StoreLocation { get; set; }

        /// <summary>
        /// Specifies the type of value that will be used to search for the x.509 certificate in the store.
        /// </summary>
        public X509FindType X509FindType { get; set; }

        /// <summary>
        /// The search criteria as an object.
        /// </summary>
        public object FindValue { get; set; }
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
