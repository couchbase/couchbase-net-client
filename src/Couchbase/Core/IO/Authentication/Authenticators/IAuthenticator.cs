#nullable enable

using System;
using System.Net.Http;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Provides a unified interface for authenticating connections to Couchbase Server.
/// Authenticators are configuration objects that store user credentials.
/// </summary>
public interface IAuthenticator
{
    /// <summary>
    /// Indicates whether this authenticator supports TLS connections.
    /// </summary>
    bool SupportsTls { get; }

    /// <summary>
    /// Indicates whether this authenticator supports non-TLS connections.
    /// </summary>
    bool SupportsNonTls { get; }

    /// <summary>
    /// Gets client certificates for mTLS authentication, if applicable.
    /// </summary>
    /// <returns>Client certificates, or null if not using certificate authentication.</returns>
    X509Certificate2Collection? GetClientCertificates(ILogger<object>? logger = null);

    /// <summary>
    /// Adds authentication to an HTTP request (e.g., Authorization header).
    /// </summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    void AuthenticateHttpRequest(HttpRequestMessage request);

    /// <summary>
    /// Adds TLS authentication to a <see cref="HttpMessageHandler"/> if the authenticator
    /// supports it (e.g. <see cref="CertificateAuthenticator"/>).
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="clusterOptions"></param>
    /// <param name="callbackFactory"></param>
    /// <param name="logger"></param>
    void AuthenticateHttpHandler(HttpMessageHandler handler, ClusterOptions clusterOptions, ICertificateValidationCallbackFactory callbackFactory, ILogger<object>? logger = null);

    /// <summary>
    /// Takes a ClientWebSocket and applies authentication (e.g., setting headers).
    /// </summary>
    /// <param name="clientWebSocket">A <see cref="ClientWebSocket"/> object.</param>
    void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket);

    /// <summary>
    /// Takes an SSL stream, applies authentication (e.g. client certificates) and connects.
    /// </summary>
    /// <param name="sslStream">The <see cref="SslStream"/> to authenticate</param>
    /// <param name="targetHost">The target host to connect to</param>
    /// <param name="clusterOptions">The <see cref="ClusterOptions"/></param>
    /// <param name="callbackFactory">An <see cref="ICertificateValidationCallbackFactory"/> implementation</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/></param>
    /// <param name="logger">An optional logger</param>
    /// <returns></returns>
    Task AuthenticateSslStream(SslStream sslStream, string targetHost, ClusterOptions clusterOptions, ICertificateValidationCallbackFactory callbackFactory, CancellationToken cancellationToken, ILogger<object>? logger = null);

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Adds authentication header for Stellar gRPC calls.
    /// </summary>
    /// <param name="metadata">Metadata to add to gRPC messages</param>
    void AuthenticateGrpcMetadata(Metadata metadata);
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
