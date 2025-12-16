#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;
using Grpc.Core;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Authenticator using username and password credentials.
/// Uses SASL for KV connections and HTTP Basic authentication for HTTP requests.
/// </summary>
public sealed class PasswordAuthenticator : BaseAuthenticator
{
    private const string BasicScheme = "Basic";
    public readonly string Username;
    public readonly string Password;
    private readonly string _encodedCredentials;
    private readonly bool _enableTls;

    public override AuthenticatorType AuthenticatorType => AuthenticatorType.Password;
    public override bool CanReauthenticateKv => false;

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
    public override bool SupportsTls => true;

    /// <inheritdoc />
    public override bool SupportsNonTls => true;

    /// <summary>
    /// Authenticates a KV connection using SASL.
    /// </summary>
    private protected override async Task AuthenticateKvConnectionCoreAsync(
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
    public override void AuthenticateHttpRequest(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(BasicScheme, _encodedCredentials);
    }

    /// <inheritdoc />
    public override void AuthenticateClientWebSocket(ClientWebSocket clientWebSocket)
    {
        clientWebSocket.Options.Credentials = new System.Net.NetworkCredential(Username, Password);
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <inheritdoc />
    public override void AuthenticateGrpcMetadata(Metadata metadata)
    {
        metadata.Add("Authorization", $"Basic {_encodedCredentials}");
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
