using System;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Default implementation of <see cref="CouchbaseHttpClientFactory"/>.
    /// </summary>
    internal class CouchbaseHttpClientFactory : ICouchbaseHttpClientFactory
    {
        private readonly ClusterContext _context;
        private readonly ILogger<CouchbaseHttpClientFactory> _logger;

        private readonly HttpMessageHandler _sharedHandler;

        public CouchbaseHttpClientFactory(ClusterContext context, ILogger<CouchbaseHttpClientFactory> logger)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (context == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(context));
            }

            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _context = context;
            _logger = logger;

            _sharedHandler = CreateClientHandler();
        }

        /// <inheritdoc />
        public HttpClient Create()
        {
            var httpClient = new HttpClient(_sharedHandler, false)
            {
                DefaultRequestHeaders =
                {
                    ExpectContinue = _context.ClusterOptions.EnableExpect100Continue
                }
            };

            ClientIdentifier.SetUserAgent(httpClient.DefaultRequestHeaders);

            return httpClient;
        }

        private HttpMessageHandler CreateClientHandler()
        {
#if !NETCOREAPP3_1_OR_GREATER
            var handler = new HttpClientHandler();

            //for x509 cert authentication
            if (_context.ClusterOptions.X509CertificateFactory != null)
            {
                handler.ClientCertificateOptions = ClientCertificateOption.Manual;
                handler.SslProtocols = _context.ClusterOptions.EnabledSslProtocols;

                handler.ClientCertificates.AddRange(_context.ClusterOptions.X509CertificateFactory.GetCertificates());
            }

            try
            {
                handler.CheckCertificateRevocationList = _context.ClusterOptions.EnableCertificateRevocation;
                handler.ServerCertificateCustomValidationCallback =
                    CreateCertificateValidator(_context.ClusterOptions);
            }
            catch (PlatformNotSupportedException)
            {
                _logger.LogDebug(
                    "Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }
            catch (NotImplementedException)
            {
                _logger.LogDebug(
                    "Cannot set ServerCertificateCustomValidationCallback, not implemented on this platform");
            }
#else
            var handler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            };

            //for x509 cert authentication
            if (_context.ClusterOptions.X509CertificateFactory != null)
            {
                handler.SslOptions.EnabledSslProtocols = _context.ClusterOptions.EnabledSslProtocols;

                var certificates = _context.ClusterOptions.X509CertificateFactory.GetCertificates();
                handler.SslOptions.ClientCertificates = certificates;

                // This emulates the behavior of HttpClientHandler in Manual mode, which selects the first certificate
                // from the list which is eligible for use as a client certificate based on having a private key and
                // the correct key usage flags.
                handler.SslOptions.LocalCertificateSelectionCallback =
                    (_, _, _, _, _) => GetClientCertificate(certificates)!;
            }

            // We don't need to check for unsupported platforms here, because this code path only applies to recent
            // versions of .NET which all support certificate validation callbacks
            handler.SslOptions.CertificateRevocationCheckMode = _context.ClusterOptions.EnableCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck;
            handler.SslOptions.RemoteCertificateValidationCallback =
                _context.ClusterOptions.HttpCertificateCallbackValidation;

            if (_context.ClusterOptions.EnabledTlsCipherSuites != null && _context.ClusterOptions.EnabledTlsCipherSuites.Count > 0)
            {
                handler.SslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(_context.ClusterOptions.EnabledTlsCipherSuites);
            }
#endif

            try
            {
                if (_context.ClusterOptions.MaxHttpConnections > 0)
                {
                    //0 means the WinHttpHandler default size of Int.MaxSize is used
                    handler.MaxConnectionsPerServer = _context.ClusterOptions.MaxHttpConnections;
                }
            }
            catch (PlatformNotSupportedException e)
            {
                _logger.LogDebug(e, "Cannot set MaxConnectionsPerServer, not supported on this platform");
            }

            var authenticatingHandler = _context.ClusterOptions.X509CertificateFactory == null
                ? (HttpMessageHandler)new AuthenticatingHttpMessageHandler(handler, _context)
                : handler;

            return authenticatingHandler;
        }

#if !NETCOREAPP3_1_OR_GREATER
        private static Func<HttpRequestMessage, X509Certificate, X509Chain, SslPolicyErrors, bool>
            CreateCertificateValidator(ClusterOptions clusterOptions)
        {
            bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate,
                X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                return clusterOptions.HttpCertificateCallbackValidation(request, certificate, chain, sslPolicyErrors);
            }

            return OnCertificateValidation;
        }
#else
        private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

        private static X509Certificate2? GetClientCertificate(X509Certificate2Collection candidateCerts) =>
            candidateCerts.Cast<X509Certificate2>()
                .FirstOrDefault(cert => cert.HasPrivateKey && IsValidClientCertificate(cert));

        private static bool IsValidClientCertificate(X509Certificate2 cert) =>
            !cert.Extensions.Cast<X509Extension>().Any(extension =>
                (extension is X509EnhancedKeyUsageExtension eku && !IsValidForClientAuthentication(eku)) ||
                (extension is X509KeyUsageExtension keyUsageExtenstion && !IsValidForDigitalSignatureUsage(keyUsageExtenstion)));

        private static bool IsValidForClientAuthentication(X509EnhancedKeyUsageExtension enhancedKeyUsageExtension) =>
            enhancedKeyUsageExtension.EnhancedKeyUsages.Cast<Oid>().Any(oid => oid.Value == ClientAuthenticationOid);

        private static bool IsValidForDigitalSignatureUsage(X509KeyUsageExtension keyUsageExtenstion) =>
            keyUsageExtenstion.KeyUsages.HasFlag(X509KeyUsageFlags.DigitalSignature);
#endif
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
