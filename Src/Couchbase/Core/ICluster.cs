
using System;
using Couchbase.Configuration.Client;
using Couchbase.Management;

namespace Couchbase.Core
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    public interface ICluster : IDisposable
    {
        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <returns>The default bucket for a Couchbase Cluster.</returns>
        IBucket OpenBucket();

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <param name="password">The password to use if it's a SASL authenticated bucket.</param>
        /// <returns>A object that implements IBucket.</returns>
        IBucket OpenBucket(string bucketname, string password);

        /// <summary>
        /// Opens a Couchbase Bucket instance.
        /// </summary>
        /// <param name="bucketname">The name of the bucket to open.</param>
        /// <returns>A object that implements IBucket.</returns>
        IBucket OpenBucket(string bucketname);

        /// <summary>
        /// Closes a Couchbase Bucket Instance.
        /// </summary>
        /// <param name="bucket">The object that implements IBucket that will be closed.</param>
        void CloseBucket(IBucket bucket);

        /// <summary>
        /// Creates a <see cref="IClusterManager"/> object that uses the current <see cref="ICluster"/> configuration settings.
        /// </summary>
        /// <returns>A <see cref="IClusterManager"/> instance that uses the current <see cref="ICluster"/> configuration settings. </returns>
        IClusterManager CreateManager(string username, string password);

        /// <summary>
        /// Returns an object which implements IClusterInfo. This object contains various server
        /// stats and information.
        /// </summary>
        [Obsolete("Use CreateManager(user, password).ClusterInfo() instead")]
        IClusterInfo Info { get; }

        ClientConfiguration Configuration { get; }

        /// <summary>
        /// Returns a response indicating whether or not the <see cref="IBucket"/> instance has been opened and this <see cref="Cluster"/> instance is observing it.
        /// </summary>
        /// <param name="bucketName">The name of the bucket to check.</param>
        /// <returns>True if the <see cref="IBucket"/> has been opened and the cluster is registered as an observer.</returns>
        bool IsOpen(string bucketName);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion