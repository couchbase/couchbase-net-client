using Couchbase.Core.IO.Authentication;
using Couchbase.Core.IO.Connections;

#nullable enable

namespace Couchbase.Core.DI
{
    internal interface ISaslMechanismFactory
    {
        ISaslMechanism CreatePasswordMechanism(MechanismType mechanismType, string username, string password);

        /// <summary>
        /// Resolves and creates the SASL mechanism for a password authenticator by negotiating against the
        /// mechanisms advertised by the server (cached on the connection during bootstrap as
        /// <see cref="IConnection.SupportedSaslMechanisms"/>).
        /// </summary>
        /// <param name="connection">The connection being authenticated; supplies the cached server mechanism list.</param>
        /// <param name="enableTls">When <c>true</c>, PLAIN is used directly (safe over TLS, no negotiation).</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>A fully-resolved SASL mechanism ready to authenticate.</returns>
        ISaslMechanism CreatePasswordMechanism(IConnection connection, bool enableTls, string username, string password);

        /// <summary>
        /// Creates an OAUTHBEARER SASL mechanism for JWT authentication.
        /// </summary>
        /// <param name="token">The JWT token.</param>
        /// <returns>An OAUTHBEARER SASL mechanism.</returns>
        ISaslMechanism CreateOAuthBearerMechanism(string token);
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
