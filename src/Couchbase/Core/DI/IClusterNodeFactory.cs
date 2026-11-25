using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Management.Buckets;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a <see cref="IClusterNode"/>.
    /// </summary>
    internal interface IClusterNodeFactory
    {
        /// <summary>
        /// Create and connect to a <see cref="IClusterNode"/>.
        /// </summary>
        /// <param name="endPoint"><see cref="HostEndpointWithPort"/> of the node.</param>
        /// <param name="nodeAdapter"></param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create and connect to a <see cref="IClusterNode"/>.
        /// </summary>
        /// <param name="endPoint"><see cref="HostEndpointWithPort"/> of the node.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a cluster node, a node with no bucket, and returns it at once. The node starts its first connect in
        /// the background, and its pool keeps trying until the node is disposed. A node with no KV service starts none.
        /// </summary>
        /// <param name="endPoint"><see cref="HostEndpointWithPort"/> of the node.</param>
        /// <param name="nodeAdapter">The entry of the node in the cluster map.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        IClusterNode CreateClusterNode(HostEndpointWithPort endPoint, NodeAdapter nodeAdapter);

        /// <summary>
        /// Create and connect to a <see cref="IClusterNode"/> that serves one bucket for life.
        /// Each of its connections selects the bucket when it opens.
        /// </summary>
        /// <param name="endPoint"><see cref="HostEndpointWithPort"/> of the node.</param>
        /// <param name="nodeAdapter"></param>
        /// <param name="bucketName">The bucket the node serves.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, string bucketName,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Create and connect to a <see cref="IClusterNode"/> that serves one bucket for life.
        /// Each of its connections selects the bucket when it opens.
        /// </summary>
        /// <param name="endPoint"><see cref="HostEndpointWithPort"/> of the node.</param>
        /// <param name="nodeAdapter"></param>
        /// <param name="bucketName">The bucket the node serves.</param>
        /// <param name="acceptRefusedBucket">When true and the node refuses the bucket, returns the node with no
        /// connection. Its pool keeps trying until the node accepts the bucket or the node is disposed.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The <seealso cref="IClusterNode"/> type.</returns>
        Task<IClusterNode> CreateAndConnectAsync(HostEndpointWithPort endPoint, NodeAdapter? nodeAdapter, string bucketName,
            bool acceptRefusedBucket, CancellationToken cancellationToken = default);
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
