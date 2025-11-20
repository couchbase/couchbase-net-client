using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.IO.Authentication.X509;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// A collection of TLS/SSL settings for Couchbase connections.
    /// </summary>
    public sealed class TlsSettings
    {
        /// <summary>
        /// Custom server CA certificates to trust (trust anchors).
        /// These are used to validate the server's certificate, NOT for client authentication.
        /// </summary>
        public ICertificateFactory? TrustedServerCertificateFactory { get; set; }

        /// <summary>
        /// Whether to ignore server certificate name mismatches for KV connections.
        /// </summary>
        public bool KvIgnoreRemoteCertificateNameMismatch { get; set; }

        /// <summary>
        /// Whether to ignore server certificate name mismatches for HTTP connections.
        /// </summary>
        public bool HttpIgnoreRemoteCertificateNameMismatch { get; set; }

        /// <summary>
        /// Custom callback for KV server certificate validation (advanced scenarios).
        /// </summary>
        public RemoteCertificateValidationCallback? KvCertificateValidationCallback { get; set; }

        /// <summary>
        /// Custom callback for HTTP server certificate validation (advanced scenarios).
        /// </summary>
        public RemoteCertificateValidationCallback? HttpCertificateValidationCallback { get; set; }

        /// <summary>
        /// Enabled SSL/TLS protocols. Defaults to TLS 1.2 and 1.3 (when supported).
        /// </summary>
#if NET5_0_OR_GREATER
        public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12 | SslProtocols.Tls13;
#else
        public SslProtocols EnabledSslProtocols { get; set; } = SslProtocols.Tls12;
#endif

        /// <summary>
        /// Whether to enable certificate revocation checking.
        /// </summary>
        public bool EnableCertificateRevocation { get; set; }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2025 Couchbase, Inc.
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
