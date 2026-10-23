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
    private readonly bool _ldapCompatible;

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
        : this(ldapCompatible: false, username, password)
    {
    }

    private PasswordAuthenticator(bool ldapCompatible, string username, string password)
    {
        Username = username;
        Password = password;
        _ldapCompatible = ldapCompatible;

        var usernameAndPasswordBytes = Encoding.UTF8.GetBytes($"{Username}:{Password}");
        _encodedCredentials = Convert.ToBase64String(usernameAndPasswordBytes);
    }

    /// <summary>
    /// Creates a PasswordAuthenticator that authenticates KV connections with SASL PLAIN only, for users
    /// that have no SCRAM credentials, such as LDAP users.
    /// </summary>
    /// <remarks>
    /// WARNING: on a non-TLS connection (<c>couchbase://</c>) PLAIN sends the password in cleartext, to anyone
    /// who can observe the network. Connect with <c>couchbases://</c> instead whenever possible. The SDK logs
    /// a warning the first time it sends PLAIN on a non-TLS connection. Over TLS, this authenticator behaves
    /// the same as one created with <see cref="PasswordAuthenticator(string, string)"/>.
    /// </remarks>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns>A PasswordAuthenticator that uses SASL PLAIN on every KV connection.</returns>
    public static PasswordAuthenticator LdapCompatible(string username, string password) =>
        new(ldapCompatible: true, username, password);

    /// <summary>
    /// Creates a new PasswordAuthenticator with the specified credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <param name="enableTls">Ignored. The SASL mechanism is chosen from each connection's transport.</param>
    /// <remarks>
    /// DEPRECATED: Use <see cref="PasswordAuthenticator(string, string)"/> instead. The
    /// <paramref name="enableTls"/> argument is ignored.
    /// </remarks>
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
        // from the SASL_LIST_MECHS list cached on the connection during bootstrap). LdapCompatible uses PLAIN
        // on every transport.
        var saslMechanism = _ldapCompatible
            ? saslMechanismFactory.CreateLdapCompatibleMechanism(connection, Username, Password)
            : saslMechanismFactory.CreatePasswordMechanism(connection, Username, Password);

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
