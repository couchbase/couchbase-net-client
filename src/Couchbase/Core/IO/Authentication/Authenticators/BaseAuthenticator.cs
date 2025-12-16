#nullable enable

using System;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Base class for authenticators that provides shared configuration logic.
/// </summary>
public abstract class BaseAuthenticator : IAuthenticator, IAuthenticatorInternal
{
    public abstract AuthenticatorType AuthenticatorType { get; }
    public abstract bool CanReauthenticateKv { get; }

    /// <inheritdoc />
    public abstract bool SupportsTls { get; }

    /// <inheritdoc />
    public abstract bool SupportsNonTls { get; }

    /// <inheritdoc />
    public virtual X509Certificate2Collection? GetClientCertificates(ILogger<object>? logger = null)
    {
        return null;
    }

    /// <inheritdoc />
    public abstract void AuthenticateHttpRequest(HttpRequestMessage request);

    /// <summary>
    /// Authenticates a KV connection using SDK-provided authentication mechanisms.
    /// </summary>
    /// <remarks>
    /// This method is private protected because it uses internal types (IConnection, ISaslMechanismFactory)
    /// and should only be accessible to derived classes within the same assembly.
    /// </remarks>
    private protected abstract Task AuthenticateKvConnectionCoreAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Explicit implementation of IAuthenticatorInternal.AuthenticateKvConnectionAsync.
    /// </summary>
    Task IAuthenticatorInternal.AuthenticateKvConnectionAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken)
    {
        return AuthenticateKvConnectionCoreAsync(connection, saslMechanismFactory, cancellationToken);
    }

    /// <summary>
    /// The base implementation configures the RemoteCertificateValidationCallback on the HttpMessageHandler.
    /// Derived classes can override to provide client certificates or other settings.
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="clusterOptions"></param>
    /// <param name="callbackFactory"></param>
    /// <param name="logger"></param>
    public virtual void AuthenticateHttpHandler(HttpMessageHandler handler, ClusterOptions clusterOptions,
        ICertificateValidationCallbackFactory callbackFactory, ILogger<object>? logger = null)
    {

#if NETCOREAPP3_1_OR_GREATER
        if (handler is SocketsHttpHandler socketsHttpHandler)
        {
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

    /// <inheritdoc />
    public abstract void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket);

    /// <inheritdoc />
    public virtual async Task AuthenticateSslStream(SslStream sslStream, string targetHost, ClusterOptions clusterOptions,
        ICertificateValidationCallbackFactory callbackFactory, CancellationToken cancellationToken, ILogger<object>? logger = null)
    {
#if NETCOREAPP3_1_OR_GREATER
        var sslOptions = new SslClientAuthenticationOptions()
        {
            TargetHost = targetHost,
            EnabledSslProtocols = clusterOptions.TlsSettings.EnabledSslProtocols,
            CertificateRevocationCheckMode = clusterOptions.TlsSettings.EnableCertificateRevocation
                ? X509RevocationMode.Online
                : X509RevocationMode.NoCheck,
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
        await sslStream.AuthenticateAsClientAsync(targetHost, null,
                clusterOptions.TlsSettings.EnabledSslProtocols,
                clusterOptions.TlsSettings.EnableCertificateRevocation)
            .ConfigureAwait(false);
#endif
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <inheritdoc />
    public abstract void AuthenticateGrpcMetadata(Metadata metadata);
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
