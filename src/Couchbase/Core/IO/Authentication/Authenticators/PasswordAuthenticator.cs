#nullable enable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Authenticator using username and password credentials.
/// Uses SASL for KV connections and HTTP Basic authentication for HTTP requests.
/// </summary>
public sealed class PasswordAuthenticator : IAuthenticator, IAuthenticatorInternal
{
    private const string BasicScheme = "Basic";
    public readonly string Username;
    public readonly string Password;
    private readonly string _encodedCredentials;
    private readonly bool _enableTls;

    /// <summary>
    /// Creates a new PasswordAuthenticator with the specified credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="enableTls">Whether TLS is enabled (affects SASL mechanism selection).</param>
    public PasswordAuthenticator(string username, string password, bool enableTls = true)
    {
        Username = username;
        Password = password;
        _enableTls = enableTls;

        var usernameAndPasswordBytes = Encoding.UTF8.GetBytes($"{Username}:{Password}");
        _encodedCredentials = Convert.ToBase64String(usernameAndPasswordBytes);
    }

    /// <inheritdoc />
    public bool SupportsTls => true;

    /// <inheritdoc />
    public bool SupportsNonTls => true;

    /// <inheritdoc />
    public X509Certificate2Collection? GetClientCertificates(ILogger<object>? logger = null)
    {
        // Password authentication does not use client certificates
        return null;
    }

    /// <summary>
    /// Authenticates a KV connection using SASL.
    /// </summary>
    async Task IAuthenticatorInternal.AuthenticateKvConnectionAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken)
    {
        // PLAIN is safe over TLS, SCRAM-SHA1 is used on insecure connections
        var mechanismType = _enableTls ? MechanismType.Plain : MechanismType.ScramSha1;

        var saslMechanism = saslMechanismFactory.CreatePasswordMechanism(mechanismType, Username, Password);

        await saslMechanism.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void AuthenticateHttpRequest(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, _encodedCredentials);
    }

    public void AuthenticateHttpHandler(HttpMessageHandler handler, ClusterOptions clusterOptions,
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
    public void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.Credentials = new NetworkCredential(Username, Password);
    }

    // The SslStream must be authenticated when TLS is enabled with Password authentication (e.g. Capella).
    // No client certificates are sent but the server certificate must be validated.
    public async Task AuthenticateSslStream(SslStream sslStream, string targetHost, ClusterOptions clusterOptions, ICertificateValidationCallbackFactory callbackFactory, CancellationToken cancellationToken, ILogger<object>? logger = null)
    {
#if NETCOREAPP3_1_OR_GREATER
        var sslOptions = new SslClientAuthenticationOptions()
        {
            TargetHost = targetHost,
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
        await sslStream.AuthenticateAsClientAsync(targetHost, null,
                clusterOptions.TlsSettings.EnabledSslProtocols,
                clusterOptions.TlsSettings.EnableCertificateRevocation)
            .ConfigureAwait(false);
#endif
    }

    public void AuthenticateGrpcMetadata(Metadata metadata)
    {
        metadata.Add("Authorization", $"Basic {_encodedCredentials}");
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
