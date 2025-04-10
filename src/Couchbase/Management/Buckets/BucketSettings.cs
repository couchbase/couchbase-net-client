using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Utils;
using Couchbase.KeyValue;
using Couchbase.Utils;

namespace Couchbase.Management.Buckets
{
    [JsonConverter(typeof(BucketSettingsJsonConverter))]
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
        /// Note: Only use this with CreateBucketAsync().
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
        /// The maximum Time-To-Live (TTL) for new documents in the Bucket.
        /// 0 : Documents do not expire.
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

        /// <summary>
        /// The type of storage to use with the bucket. This is only specified for "couchbase" buckets.
        /// </summary>
        [InterfaceStability(Level.Uncommitted)]
        public StorageBackend? StorageBackend { get; set; }

        /// <summary>
        /// The number of vBuckets the bucket should have. If not set, the server default will be used.
        /// Refer to the Server documentation for the <see cref="StorageBackend"/> for more information on valid numVBuckets values.
        /// </summary>
        [InterfaceStability(Level.Volatile)]
        public uint? NumVBuckets { get; set; }

        /// <summary>
        /// Whether to enable history retention on collections by default.
        /// </summary>
        public bool? HistoryRetentionCollectionDefault { get; set; }

        /// <summary>
        /// The maximum size, in bytes, of the change history that is written to disk for all collections in this bucket.
        /// </summary>
        public ulong? HistoryRetentionBytes { get; set; }

        /// <summary>
        /// The maximum duration of history each vBucket should aim to retain on disk.
        /// </summary>
        public TimeSpan? HistoryRetentionDuration { get; set; }

        /// <summary>
        /// Validates the settings and creates a list of name value pairs to send to the server as form values.
        /// </summary>
        /// <returns></returns>
        internal IReadOnlyDictionary<string, string> ToFormValues()
        {
            var settings = this;
            var values = new Dictionary<string, string>
            {
                {"name", settings.Name},
                {"bucketType", settings.BucketType.GetDescription()},
                {"ramQuotaMB", settings.RamQuotaMB.ToStringInvariant()},
                {"flushEnabled", settings.FlushEnabled ? "1" : "0"}
            };

            if (HistoryRetentionCollectionDefault.HasValue)
            {
                values.Add("historyRetentionCollectionDefault", HistoryRetentionCollectionDefault.Value.ToLowerString());
            }

            if (HistoryRetentionBytes is > 0)
            {
                values.Add("historyRetentionBytes", HistoryRetentionBytes.Value.ToString());
            }

            if (HistoryRetentionDuration.HasValue)
            {
                values.Add("historyRetentionSeconds", HistoryRetentionDuration.Value.TotalSeconds.ToString());
            }

            if (settings.BucketType != BucketType.Memcached)
            {
                values.Add("replicaNumber", settings.NumReplicas.ToStringInvariant());
            }

            if (settings.BucketType == BucketType.Couchbase)
            {
                values.Add("replicaIndex", settings.ReplicaIndexes ? "1" : "0");
            }

            if (settings.ConflictResolutionType.HasValue)
            {
                values.Add("conflictResolutionType", settings.ConflictResolutionType.GetDescription());
            }

            /*Policy-assignment depends on bucket type. For a Couchbase bucket, the policy can be valueOnly (which is the default)
                or fullEviction. For an Ephemeral bucket, the policy can be noEviction (which is the default) or nruEviction. No policy
                can be assigned to a Memcached bucket.*/

            if (settings.EvictionPolicy.HasValue)
            {
                if (settings.BucketType == BucketType.Couchbase)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.NoEviction ||
                        settings.EvictionPolicy == EvictionPolicyType.NotRecentlyUsed)
                    {
                        throw new InvalidArgumentException(
                            "For a Couchbase bucket, the eviction policy can be valueOnly (which is the default) or fullEviction.");
                    }
                }

                if (settings.BucketType == BucketType.Ephemeral)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.ValueOnly ||
                        settings.EvictionPolicy == EvictionPolicyType.FullEviction)
                    {
                        throw new InvalidArgumentException(
                            "For an Ephemeral bucket, the eviction policy can be noEviction (which is the default) or nruEviction.");
                    }
                }

                if (settings.BucketType == BucketType.Memcached)
                {
                    throw new InvalidArgumentException("No eviction policy can be assigned to a Memcached bucket.");
                }

                values.Add("evictionPolicy", settings.EvictionPolicy.GetDescription());
            }

            if (settings.MaxTtl > 0)
            {
                values.Add("maxTTL", settings.MaxTtl.ToStringInvariant());
            }

            if (settings.CompressionMode.HasValue)
            {
                values.Add("compressionMode", settings.CompressionMode.GetDescription());
            }

            if (settings.DurabilityMinimumLevel != DurabilityLevel.None)
            {
                values.Add("durabilityMinLevel", settings.DurabilityMinimumLevel.GetDescription());
            }

            if (settings.StorageBackend.HasValue)
            {
                values.Add("storageBackend", settings.StorageBackend.GetDescription());
            }

            if (settings.NumVBuckets.HasValue)
            {
                values.Add("numVBuckets", settings.NumVBuckets.Value.ToStringInvariant());
            }

            return values;
        }
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
