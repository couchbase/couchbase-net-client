
using System;
using Couchbase.Configuration.Client;

namespace Couchbase.Core
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    internal interface ICouchbaseCluster : IDisposable
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
        /// Returns an object which implements IClusterInfo. This object contains various server
        /// stats and information.
        /// </summary>
        IClusterInfo Info { get; }

        ClientConfiguration Configuration { get; }
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