using System;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Creates the server certificate validation callback for KV and HTTP connections.
    /// </summary>
    internal sealed class CertificateValidationCallbackFactory : ICertificateValidationCallbackFactory
    {
        private readonly ILogger<CertificateValidationCallbackFactory> _logger;
        private readonly IRedactor _redactor;
        private readonly TlsSettings _tlsSettings;

        public CertificateValidationCallbackFactory(
            ClusterOptions clusterOptions,
            ILogger<CertificateValidationCallbackFactory> logger,
            IRedactor redactor)
        {
            _tlsSettings = clusterOptions?.TlsSettings ?? throw new ArgumentNullException(nameof(clusterOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        public RemoteCertificateValidationCallback CreateForKv() =>
            Create(_tlsSettings.KvCertificateValidationCallback, _tlsSettings.KvIgnoreRemoteCertificateNameMismatch);

        public RemoteCertificateValidationCallback CreateForHttp() =>
            Create(_tlsSettings.HttpCertificateValidationCallback, _tlsSettings.HttpIgnoreRemoteCertificateNameMismatch);

        private RemoteCertificateValidationCallback Create(RemoteCertificateValidationCallback? userCallback, bool ignoreNameMismatch)
        {
            if (userCallback != null)
            {
                return userCallback;
            }

            // Resolved on every call so a trusted certificate factory that rotates is honoured by new connections.
            var trustedCertificates = _tlsSettings.TrustedServerCertificateFactory?.GetCertificates()
                                      ?? new X509Certificate2Collection(CertificateFactory.DefaultCertificates.ToArray());

            var validator = new ServerCertificateValidator(
                trustedCertificates,
                ignoreNameMismatch,
                _tlsSettings.EnableCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                _logger,
                _redactor);

            return validator.Validate;
        }
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
