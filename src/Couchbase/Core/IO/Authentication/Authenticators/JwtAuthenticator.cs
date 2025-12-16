#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Authenticator using JSON Web Tokens (JWT).
/// Uses SASL OAUTHBEARER for KV connections and Bearer token for HTTP requests.
/// </summary>
[InterfaceStability(Level.Uncommitted)]
public sealed class JwtAuthenticator : BaseAuthenticator
{
    private const string BearerScheme = "Bearer";
    public readonly string Token;

    public override AuthenticatorType AuthenticatorType => AuthenticatorType.Jwt;

    public override bool CanReauthenticateKv => true;

    /// <summary>
    /// Creates a new JwtAuthenticator with the specified token.
    /// </summary>
    /// <param name="token">The JSON Web Token.</param>
    public JwtAuthenticator(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Token cannot be null or empty", nameof(token));
        }

        Token = token;
    }

    /// <inheritdoc />
    public override bool SupportsTls => true;

    /// <inheritdoc />
    public override bool SupportsNonTls => false;

    /// <summary>
    /// Authenticates a KV connection using SASL OAUTHBEARER.
    /// </summary>
    private protected override async Task AuthenticateKvConnectionCoreAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken)
    {
        var mechanism = saslMechanismFactory.CreateOAuthBearerMechanism(Token);
        await mechanism.AuthenticateAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void AuthenticateHttpRequest(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, Token);
    }

    /// <inheritdoc />
    public override void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.SetRequestHeader("Authorization", $"Bearer {Token}");
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <inheritdoc />
    public override void AuthenticateGrpcMetadata(Grpc.Core.Metadata metadata)
    {
        metadata.Add("Authorization", $"Bearer {Token}");
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
