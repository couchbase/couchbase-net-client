using System.Net;

#nullable enable

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Represents a VBucket partition in a Couchbase cluster
    /// </summary>
    internal interface IVBucket : IMappedNode
    {
        /// <summary>
        /// Locates a replica for a given index.
        /// </summary>
        /// <param name="index">The index of the replica.</param>
        /// <returns>An <see cref="IServer"/> if the replica is found, otherwise null.</returns>
        HostEndpointWithPort? LocateReplica(short index);

        /// <summary>
        /// Gets an array of replica indexes.
        /// </summary>
        short[] Replicas { get; }

        /// <summary>
        /// Gets the index of the VBucket.
        /// </summary>
        /// <value>
        /// The index.
        /// </value>
        short Index { get; }

        /// <summary>
        /// Gets the index of the primary node in the VBucket.
        /// </summary>
        /// <value>
        /// The primary index that the key has mapped to.
        /// </value>
        short Primary { get; }

        /// <summary>
        /// Gets a value indicating whether this instance has replicas.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance has replicas; otherwise, <c>false</c>.
        /// </value>
        bool HasReplicas { get; }

        /// <summary>
        /// Name of the bucket this vBucket is associated with.
        /// </summary>
        string BucketName { get; }
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
