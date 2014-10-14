using System;
using Couchbase.Configuration.Client;
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

        IKeyMapper GetKeyMapper();

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

        /// <summary>
        /// Loads the most updated configuration creating any resources as needed. The <see cref="IBucketConfig"/>
        /// used by this method is passed into the CTOR.
        /// </summary>
        /// <remarks>This method should be called immediately after creation.</remarks>
        void LoadConfig();

        /// <summary>
        /// Loads the most updated configuration creating any resources as needed based upon the passed in  <see cref="IBucketConfig"/>.
        /// </summary>
        void LoadConfig(IBucketConfig bucketConfig, bool force=false);
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