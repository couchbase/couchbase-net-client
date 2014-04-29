
using System;

namespace Couchbase.Core
{
    /// <summary>
    /// The client interface to a Couchbase Server Cluster.
    /// </summary>
    internal interface ICluster : IDisposable
    {
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
    }
}
