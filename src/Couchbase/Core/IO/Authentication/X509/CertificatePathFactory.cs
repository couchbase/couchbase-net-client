using System.Security.Cryptography.X509Certificates;

namespace Couchbase.Core.IO.Authentication.X509
{
    /// <summary>
    /// A CertificateFactory that looks up an x509 given a directory path and password.
    /// </summary>
    public class CertificatePathFactory : ICertificateFactory
    {
        private readonly string _path;
        private readonly string _password;

        public CertificatePathFactory(string path, string password)
        {
            _path = path;
            _password = password;
        }

        public X509Certificate2Collection GetCertificates()
        {
            return new X509Certificate2Collection(new X509Certificate2(_path, _password));
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
