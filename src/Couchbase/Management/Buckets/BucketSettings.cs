using System;
using Couchbase.KeyValue;

namespace Couchbase.Management.Buckets
{
    public class BucketSettings
    {
        /// <summary>
        /// The bucket name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The <see cref="BucketType"/> type to be created.
        /// </summary>
        public BucketType BucketType { get; set; } = BucketType.Couchbase;

        /// <summary>
        /// The amount of RAM to allocate for the bucket.
        /// </summary>
        public long RamQuotaMB { get; set; }

        /// <summary>
        /// Enables flushing on the bucket.
        /// </summary>
        public bool FlushEnabled { get; set; }

        /// <summary>
        /// The number of servers that a document will be replicated to.
        /// </summary>
        public int NumReplicas { get; set; }

        /// <summary>
        /// Whether or not to replicate indexes across the cluster.
        /// </summary>
        public bool ReplicaIndexes { get; set; }

        /// <summary>
        /// The type of conflict resolution to use.
        /// </summary>
        public ConflictResolutionType? ConflictResolutionType { get; set; }

        /// <summary>
        /// The <see cref="EvictionPolicy"/> to use.
        /// </summary>
        public EvictionPolicyType? EvictionPolicy { get; set; }

        [Obsolete("Use EvictionPolicy instead.")]
        public EvictionPolicyType? EjectionMethod
        {
            get => EvictionPolicy;
            set => EvictionPolicy = value;
        }

        /// <summary>
        /// The max time-to-live for documents in the bucket.
        /// </summary>
        public int MaxTtl { get; set; }

        /// <summary>
        /// The <see cref="CompressionMode"/> to use.
        /// </summary>
        public CompressionMode? CompressionMode { get; set; }

        /// <summary>
        /// Returns the minimum durability level set for the bucket.
        /// </summary>
        /// <remarks>Note that if the bucket does not support it, and by default, it is set to <see cref="DurabilityLevel.None"/>.</remarks>
        public DurabilityLevel DurabilityMinimumLevel { get; set; } = DurabilityLevel.None;
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
