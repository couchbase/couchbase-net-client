#nullable enable

using System;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Authentication.X509;
using Couchbase.Core.IO.Connections;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Authenticator using X.509 client certificates for mTLS.
/// </summary>
public sealed class CertificateAuthenticator : IAuthenticator, IAuthenticatorInternal
{
    private readonly ICertificateFactory _certificateFactory;

    /// <summary>
    /// Creates a new CertificateAuthenticator with the specified certificate factory.
    /// Note: Only provide client certificates that are intended for authentication.
    /// Server CAs and Trust Anchors should be provided in <see cref="TlsSettings.TrustedServerCertificateFactory"/>
    /// </summary>
    /// <param name="certificateFactory">Factory for providing client certificates.</param>
    public CertificateAuthenticator(ICertificateFactory certificateFactory)
    {
        _certificateFactory = certificateFactory ?? throw new ArgumentNullException(nameof(certificateFactory));
    }

    /// <summary>
    /// Gets the certificate factory used by this authenticator.
    /// </summary>
    public ICertificateFactory CertificateFactory => _certificateFactory;

    /// <inheritdoc />
    public bool SupportsTls => true;

    /// <inheritdoc />
    public bool SupportsNonTls => false;

    /// <inheritdoc />
    public X509Certificate2Collection? GetClientCertificates(ILogger<object>? logger = null)
    {
        // Provide client certificates for TLS handshake
        var clientCerts = _certificateFactory.GetCertificates();
        if (logger is null || clientCerts is not { Count: > 0 } || !logger.IsEnabled(LogLevel.Debug)) return clientCerts;

        foreach (var cert in clientCerts)
        {
            logger.LogDebug("Using client cert {FriendlyName} - Thumbprint {Thumbprint}", cert.FriendlyName,
                cert.Thumbprint);
        }

        logger.LogDebug("Using {Count} client certificates", clientCerts.Count);

        return clientCerts;
    }

    /// <summary>
    /// Authenticates a KV connection (no-op for certificate auth as authentication happens during TLS handshake).
    /// </summary>
    Task IAuthenticatorInternal.AuthenticateKvConnectionAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken)
    {
        // No SASL authentication needed - authentication happens in SSL Stream
        // The server authenticates the client based on the certificate presented
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void AuthenticateHttpRequest(HttpRequestMessage request)
    {
        // No HTTP header needed - authentication happens during TLS handshake
        // The client certificate is provided during connection establishment
    }

    public void AuthenticateHttpHandler(HttpMessageHandler handler, ClusterOptions clusterOptions, ICertificateValidationCallbackFactory callbackFactory, ILogger<object>? logger = null)
    {
#if NETCOREAPP3_1_OR_GREATER
        if (handler is SocketsHttpHandler socketsHttpHandler)
        {
            var clientCerts = GetClientCertificates();
            if (clientCerts is { Count: > 0 })
            {
                socketsHttpHandler.SslOptions.EnabledSslProtocols = clusterOptions.TlsSettings.EnabledSslProtocols;
                socketsHttpHandler.SslOptions.ClientCertificates = clientCerts;
            }

            socketsHttpHandler.SslOptions.CertificateRevocationCheckMode = clusterOptions.TlsSettings.EnableCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck;

            socketsHttpHandler.SslOptions.RemoteCertificateValidationCallback = callbackFactory.CreateForHttp();

            if (clusterOptions.PlatformSupportsCipherSuite && clusterOptions.EnabledTlsCipherSuites is { Count: > 0 })
            {
                socketsHttpHandler.SslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(clusterOptions.EnabledTlsCipherSuites);
            }
        }
#else
        if (handler is HttpClientHandler httpClientHandler)
        {
            var clientCerts = GetClientCertificates(logger);
            if (clientCerts is { Count: > 0 })
            {
                httpClientHandler.ClientCertificateOptions = ClientCertificateOption.Manual;
                httpClientHandler.ClientCertificates.AddRange(clientCerts);
            }

            try
            {
                httpClientHandler.SslProtocols = clusterOptions.TlsSettings.EnabledSslProtocols;
                httpClientHandler.ServerCertificateCustomValidationCallback = CreateCertificateValidator();
            }
            catch (PlatformNotSupportedException)
            {
                logger?.LogDebug(
                    "Cannot set ServerCertificateCustomValidationCallback, not supported on this platform");
            }
            catch (NotImplementedException)
            {
                logger?.LogDebug(
                    "Cannot set ServerCertificateCustomValidationCallback, not implemented on this platform");
            }

            // Local function to create the remote certificate validator,
            // as HttpClientHandler.ServerCertificateCustomValidationCallback is
            // not of type RemoteCertificateValidationCallback
            Func<HttpRequestMessage, X509Certificate, X509Chain, SslPolicyErrors, bool> CreateCertificateValidator()
            {
                return OnCertificateValidation;

                bool OnCertificateValidation(HttpRequestMessage request, X509Certificate certificate,
                    X509Chain chain, SslPolicyErrors sslPolicyErrors)
                {
                    // Use the factory to create an HTTP-specific callback
                    var callback = callbackFactory.CreateForHttp();
                    return callback(request, certificate, chain, sslPolicyErrors);
                }
            }
        }
#endif
    }

    public void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.ClientCertificates = _certificateFactory.GetCertificates();
    }

    public async Task AuthenticateSslStream(SslStream sslStream, string targetHost, ClusterOptions clusterOptions, ICertificateValidationCallbackFactory callbackFactory, CancellationToken cancellationToken = default, ILogger<object>? logger = null)
    {
        var clientCerts = GetClientCertificates(logger);
#if NETCOREAPP3_1_OR_GREATER
        var sslOptions = new SslClientAuthenticationOptions()
        {
            TargetHost = targetHost,
            ClientCertificates = clientCerts,
            EnabledSslProtocols = clusterOptions.TlsSettings.EnabledSslProtocols,
            CertificateRevocationCheckMode = clusterOptions.TlsSettings.EnableCertificateRevocation ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
            RemoteCertificateValidationCallback = callbackFactory.CreateForKv()
        };
        if (clusterOptions.PlatformSupportsCipherSuite
            && clusterOptions.EnabledTlsCipherSuites != null
            && clusterOptions.EnabledTlsCipherSuites.Count > 0)
        {
            sslOptions.CipherSuitesPolicy = new CipherSuitesPolicy(clusterOptions.EnabledTlsCipherSuites);
        }

        await sslStream.AuthenticateAsClientAsync(sslOptions, cancellationToken).ConfigureAwait(false);
#else
        await sslStream.AuthenticateAsClientAsync(targetHost, clientCerts,
                clusterOptions.TlsSettings.EnabledSslProtocols,
                clusterOptions.TlsSettings.EnableCertificateRevocation)
            .ConfigureAwait(false);
#endif
    }

#if NETCOREAPP3_1_OR_GREATER
    public void AuthenticateGrpcMetadata(Metadata metadata)
    {
        // No need to add headers, SocketsHttpsHandler contains client certificates
    }
#endif
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
