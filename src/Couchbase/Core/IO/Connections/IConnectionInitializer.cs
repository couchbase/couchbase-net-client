using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Interface for initializing a newly connected <see cref="IConnection"/>.
    /// Primarily used by <see cref="IConnectionPool"/> implementations to support
    /// creating new connections as needed.
    /// </summary>
    /// <seealso cref="ClusterNode"/>.
    internal interface IConnectionInitializer
    {
        /// <summary>
        /// <see cref="IPEndPoint"/> of the node being connected to.
        /// </summary>
        IPEndPoint EndPoint { get; }

        /// <summary>
        /// <see cref="HostEndpoint"/> of the node being connected to. Used for authenticating as the target host when TLS is enabled.
        /// </summary>
        HostEndpoint BootstrapEndpoint { get; }

        /// <summary>
        /// Initializes and authenticates a new <see cref="IConnection"/>.
        /// </summary>
        /// <param name="connection">A newly connected <see cref="IConnection"/>.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>Task to observe for completion.</remarks>
        Task InitializeConnectionAsync(IConnection connection, CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects the active bucket on an <see cref="IConnection"/>.
        /// </summary>
        /// <param name="connection">An active and initialized <see cref="IConnection"/>.</param>
        /// <param name="name">Name of the bucket.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>Task to observe for completion.</remarks>
        Task SelectBucketAsync(IConnection connection, string name, CancellationToken cancellationToken = default);
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
