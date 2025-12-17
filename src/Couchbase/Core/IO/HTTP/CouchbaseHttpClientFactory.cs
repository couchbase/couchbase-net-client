using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Runtime.CompilerServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Connections;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.HTTP
{
    /// <summary>
    /// Default implementation of <see cref="CouchbaseHttpClientFactory"/>.
    /// </summary>
    internal sealed class CouchbaseHttpClientFactory : ICouchbaseHttpClientFactory
    {
        private readonly ClusterContext _context;
        private readonly ILogger<CouchbaseHttpClientFactory> _logger;
        private readonly IRedactor _redactor;
        internal volatile HttpMessageHandler _sharedHandler;

        public CouchbaseHttpClientFactory(ClusterContext context, ILogger<CouchbaseHttpClientFactory> logger, IRedactor redactor)
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

            if (redactor == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(redactor));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            _context = context;
            _logger = logger;
            _redactor = redactor;

            DefaultCompletionOption = _context.ClusterOptions.Tuning.StreamHttpResponseBodies
                ? HttpCompletionOption.ResponseHeadersRead
                : HttpCompletionOption.ResponseContentRead;

            _sharedHandler = CreateClientHandler();
        }

        /// <inheritdoc />
        public HttpCompletionOption DefaultCompletionOption { get; }

        public HttpMessageHandler Handler => _sharedHandler;

        /// <inheritdoc />
        public HttpClient Create()
        {
            //check for cert updates if were using a rotating cert factory
            if (_context.ClusterOptions.X509CertificateFactory is
                IRotatingCertificateFactory {
                    HasUpdates: true
                })
            {
                //this may pull the rug from in-progress requests
                _sharedHandler.Dispose();
                _sharedHandler = CreateClientHandler();
            }

            var httpClient = new HttpClient(_sharedHandler, false)
            {
                DefaultRequestHeaders =
                {
                    ExpectContinue = _context.ClusterOptions.EnableExpect100Continue
                }
            };

#if NET5_0_OR_GREATER
            //experimental support for HTTP V.2
            if (_context.ClusterOptions.Experiments.EnableHttpVersion2)
            {
                httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                httpClient.DefaultRequestVersion = HttpVersion.Version20;
            }
#endif

            ClientIdentifier.SetUserAgent(httpClient.DefaultRequestHeaders);

            return httpClient;
        }

        private HttpMessageHandler CreateClientHandler()
        {
            var clusterOptions = _context.ClusterOptions;

            if (clusterOptions.IsCapella && !clusterOptions.EffectiveEnableTls)
            {
                _logger.LogWarning("TLS is required when connecting to Couchbase Capella. Please enable TLS by prefixing the connection string with \"couchbases://\" (note the final 's').");
            }

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
            var handler = new SocketsHttpHandler();

            X509Certificate2Collection? certs = null;
            //for x509 cert authentication
            if (_context.ClusterOptions.X509CertificateFactory != null)
            {
                handler.SslOptions.EnabledSslProtocols = _context.ClusterOptions.EnabledSslProtocols;

                certs = _context.ClusterOptions.X509CertificateFactory.GetCertificates();
                handler.SslOptions.ClientCertificates = certs;

                // This emulates the behavior of HttpClientHandler in Manual mode, which selects the first certificate
                // from the list which is eligible for use as a client certificate based on having a private key and
                // the correct key usage flags.
                handler.SslOptions.LocalCertificateSelectionCallback =
                    (_, _, _, _, _) => GetClientCertificate(certs)!;
            }

            // We don't need to check for unsupported platforms here, because this code path only applies to recent
            // versions of .NET which all support certificate validation callbacks
            handler.SslOptions.CertificateRevocationCheckMode = _context.ClusterOptions.EnableCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck;

            RemoteCertificateValidationCallback? certValidationCallback = _context.ClusterOptions.HttpCertificateCallbackValidation;
            if (certValidationCallback == null)
            {
                CallbackCreator callbackCreator = new CallbackCreator( _context.ClusterOptions.HttpIgnoreRemoteCertificateMismatch, _logger, _redactor, certs);
                certValidationCallback = certValidationCallback = (__sender, __certificate, __chain, __sslPolicyErrors) =>
                    callbackCreator.Callback(__sender, __certificate, __chain, __sslPolicyErrors);
            }

            handler.SslOptions.RemoteCertificateValidationCallback = certValidationCallback;

            if (_context.ClusterOptions.PlatformSupportsCipherSuite
                && _context.ClusterOptions.EnabledTlsCipherSuites.Count > 0)
            {
                handler.SslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(_context.ClusterOptions.EnabledTlsCipherSuites);
            }

            if (_context.ClusterOptions.IdleHttpConnectionTimeout > TimeSpan.Zero)
            {
                //https://issues.couchbase.com/browse/MB-37032
                handler.PooledConnectionIdleTimeout = _context.ClusterOptions.IdleHttpConnectionTimeout;
            }

            if (_context.ClusterOptions.HttpConnectionLifetime > TimeSpan.Zero)
            {
                handler.PooledConnectionLifetime = _context.ClusterOptions.HttpConnectionLifetime;
            }

#endif

#if NET5_0_OR_GREATER
            if (_context.ClusterOptions.EnableTcpKeepAlives)
            {
                handler.KeepAlivePingDelay = _context.ClusterOptions.TcpKeepAliveInterval;
                handler.KeepAlivePingTimeout = _context.ClusterOptions.TcpKeepAliveTime;
                handler.KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always;
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

            return new AuthenticatingHttpMessageHandler(handler, _context);
        }

#if !NETCOREAPP3_1_OR_GREATER
        private Func<HttpRequestMessage, X509Certificate, X509Chain, SslPolicyErrors, bool>
            CreateCertificateValidator(ClusterOptions clusterOptions)
        {
            bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate,
                X509Chain chain, SslPolicyErrors sslPolicyErrors)
            {
                var callback = clusterOptions.HttpCertificateCallbackValidation;
                if (callback == null)
                {
                    CallbackCreator callbackCreator = new CallbackCreator(clusterOptions.HttpIgnoreRemoteCertificateMismatch, _logger, _redactor, null);
                    callback = (__sender, __certificate, __chain, __sslPolicyErrors) =>
                        callbackCreator.Callback(__sender, __certificate, __chain, __sslPolicyErrors);
                }
                return callback(request, certificate, chain, sslPolicyErrors);
            }

            return OnCertificateValidation;
        }
#else
        private const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

        internal static X509Certificate2? GetClientCertificate(X509Certificate2Collection candidateCerts) =>
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
