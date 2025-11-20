using System;
using System.Net.Security;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Factory for creating server certificate validation callbacks for KV and HTTP connections.
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

        /// <summary>
        /// Creates a certificate validation callback for KV connections.
        /// </summary>
        /// <returns>A RemoteCertificateValidationCallback for KV connections.</returns>
        public RemoteCertificateValidationCallback CreateForKv()
        {
            if (_tlsSettings == null)
            {
                throw new ArgumentNullException(nameof(_tlsSettings));
            }

            // If custom callback is provided, use it
            if (_tlsSettings.KvCertificateValidationCallback != null)
            {
                return _tlsSettings.KvCertificateValidationCallback;
            }

            // Otherwise create default callback
            var callbackCreator = new CallbackCreator(
                _tlsSettings.KvIgnoreRemoteCertificateNameMismatch,
                _logger,
                _redactor,
                _tlsSettings.TrustedServerCertificateFactory?.GetCertificates());

            return (sender, certificate, chain, sslPolicyErrors) =>
                callbackCreator.Callback(sender, certificate, chain, sslPolicyErrors);
        }

        /// <summary>
        /// Creates a certificate validation callback for HTTP connections.
        /// </summary>
        /// <returns>A RemoteCertificateValidationCallback for HTTP connections.</returns>
        public RemoteCertificateValidationCallback CreateForHttp()
        {
            if (_tlsSettings == null)
            {
                throw new ArgumentNullException(nameof(_tlsSettings));
            }

            // If custom callback is provided, use it
            if (_tlsSettings.HttpCertificateValidationCallback != null)
            {
                return _tlsSettings.HttpCertificateValidationCallback;
            }

            // Otherwise create default callback
            var callbackCreator = new CallbackCreator(
                _tlsSettings.HttpIgnoreRemoteCertificateNameMismatch,
                _logger,
                _redactor,
                _tlsSettings.TrustedServerCertificateFactory?.GetCertificates());

            return (sender, certificate, chain, sslPolicyErrors) =>
                callbackCreator.Callback(sender, certificate, chain, sslPolicyErrors);
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
