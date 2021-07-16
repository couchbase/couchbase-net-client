using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Connections;

#nullable enable

namespace Couchbase.Core.IO.Authentication
{
    /// <summary>
    /// Provides and interface for implementing a SASL authentication mechanism (CRAM MD5 or PLAIN).
    /// </summary>
    internal interface ISaslMechanism
    {
        /// <summary>
        /// The type of SASL mechanism to use: PLAIN, CRAM MD5, etc.
        /// </summary>
        MechanismType MechanismType { get; }

        /// <summary>
        /// Authenticates a username and password.
        /// </summary>
        /// <param name="connection">An implementation of <see cref="IConnection"/> which represents a TCP connection to a Couchbase Server.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if successful.</returns>
        Task AuthenticateAsync(IConnection connection, CancellationToken cancellationToken = default);
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
