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

    public override AuthenticatorType AuthenticatorType => AuthenticatorType.Password;
    public override bool CanReauthenticateKv => false;

    /// <summary>
    /// Creates a new PasswordAuthenticator with the specified credentials.
    /// </summary>
    /// <remarks>
    /// The SASL mechanism is chosen for each KV connection from that connection's transport: PLAIN over
    /// TLS, otherwise the strongest SCRAM mechanism the server supports.
    /// </remarks>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    public PasswordAuthenticator(string username, string password)
    {
        Username = username;
        Password = password;

        var usernameAndPasswordBytes = Encoding.UTF8.GetBytes($"{Username}:{Password}");
        _encodedCredentials = Convert.ToBase64String(usernameAndPasswordBytes);
    }

    /// <summary>
    /// Creates a new PasswordAuthenticator with the specified credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="enableTls">Ignored. The SASL mechanism is chosen from each connection's transport.</param>
    [Obsolete("enableTls is ignored; the SASL mechanism is chosen from each connection's transport. Use PasswordAuthenticator(username, password).")]
    public PasswordAuthenticator(string username, string password, bool enableTls)
        : this(username, password)
    {
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
        // The factory resolves the mechanism from the connection's transport: PLAIN over TLS, otherwise the
        // strongest SCRAM mechanism mutually supported by this target framework and the server (negotiated
        // from the SASL_LIST_MECHS list cached on the connection during bootstrap).
        var saslMechanism = saslMechanismFactory.CreatePasswordMechanism(
            connection, Username, Password);

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
