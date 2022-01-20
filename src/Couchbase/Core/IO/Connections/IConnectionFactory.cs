using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Creates and connects an <see cref="IConnection"/>.
    /// </summary>
    internal interface IConnectionFactory
    {
        /// <summary>
        /// Creates and connects an <see cref="IConnection"/>.
        /// </summary>
        /// <param name="hostEndpoint">Endpoint to connect.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The new <see cref="IConnection"/>.</returns>
        Task<IConnection> CreateAndConnectAsync(HostEndpointWithPort hostEndpoint, CancellationToken cancellationToken = default);
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
