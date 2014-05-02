using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Provides an interface for implementing an object responsible for maintaining a 
    /// list of nodes in cluster and the mapping between keys and nodes.
    /// </summary>
    internal interface IConfigInfo : IDisposable
    {
        /// <summary>
        /// The time at which this configuration context has been created.
        /// </summary>
        DateTime CreationTime { get; }

        IKeyMapper GetKeyMapper(string bucketName);

        IServer GetServer();

        /// <summary>
        /// The client configuration used for bootstrapping.
        /// </summary>
        ClientConfiguration ClientConfig { get; }

        /// <summary>
        /// The client configuration for a bucket.
        /// <remarks> See <see cref="IBucketConfig"/> for details.</remarks>
        /// </summary>
        IBucketConfig BucketConfig { get; }

        /// <summary>
        /// The name of the Bucket that this configuration represents.
        /// </summary>
        string BucketName { get; }

        /// <summary>
        /// The <see cref="BucketTypeEnum"/> that this configuration context is for.
        /// </summary>
        BucketTypeEnum BucketType { get; }

        /// <summary>
        /// The <see cref="NodeLocatorEnum"/> that this configuration is using.
        /// </summary>
        NodeLocatorEnum NodeLocator { get; }
    }
}
