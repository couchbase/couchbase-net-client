#nullable enable

using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Connections;

namespace Couchbase.Core.IO.Authentication.Authenticators;

/// <summary>
/// Internal extensions for IAuthenticator that provide SDK-internal functionality.
/// These methods use SDK-internal types and are not exposed to end users.
/// </summary>
internal static class IAuthenticatorExtensions
{
    /// <summary>
    /// Authenticates a KV connection using SDK-provided authentication mechanisms.
    /// This is an internal extension method that allows the SDK to pass internal types
    /// without exposing them in the public API.
    /// </summary>
    /// <param name="authenticator">The authenticator.</param>
    /// <param name="connection">The connection to authenticate.</param>
    /// <param name="saslMechanismFactory">SDK-provided factory for creating SASL mechanisms.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task representing the authentication operation.</returns>
    public static Task AuthenticateKvConnectionAsync(
        this IAuthenticator authenticator,
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken)
    {
        // Cast to internal interface that has the method implementation
        if (authenticator is IAuthenticatorInternal internalAuth)
        {
            return internalAuth.AuthenticateKvConnectionAsync(connection, saslMechanismFactory, cancellationToken);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Internal interface for authenticator implementations that provides access to SDK-internal types.
/// </summary>
internal interface IAuthenticatorInternal
{
    /// <summary>
    /// Authenticates a KV connection using SDK-provided authentication mechanisms.
    /// </summary>
    Task AuthenticateKvConnectionAsync(
        IConnection connection,
        ISaslMechanismFactory saslMechanismFactory,
        CancellationToken cancellationToken);
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
