using Couchbase.Core.IO.Authentication;

#nullable enable

namespace Couchbase.Core.DI
{
    internal interface ISaslMechanismFactory
    {
        ISaslMechanism CreatePasswordMechanism(MechanismType mechanismType, string username, string password);

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
