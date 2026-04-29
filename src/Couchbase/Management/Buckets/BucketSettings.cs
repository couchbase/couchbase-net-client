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
        private readonly HashSet<string> _setProperties = new();

        private BucketType _bucketType = Buckets.BucketType.Couchbase;
        private long _ramQuotaMB;
        private bool _flushEnabled;
        private int _numReplicas;
        private bool _replicaIndexes;
        private int _maxTtl;
        private DurabilityLevel _durabilityMinimumLevel = DurabilityLevel.None;

        /// <summary>
        /// The bucket name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The <see cref="BucketType"/> type to be created.
        /// </summary>
        public BucketType BucketType
        {
            get => _bucketType;
            set { _bucketType = value; _setProperties.Add(nameof(BucketType)); }
        }

        /// <summary>
        /// The amount of RAM to allocate for the bucket.
        /// </summary>
        public long RamQuotaMB
        {
            get => _ramQuotaMB;
            set { _ramQuotaMB = value; _setProperties.Add(nameof(RamQuotaMB)); }
        }

        /// <summary>
        /// Enables flushing on the bucket.
        /// </summary>
        public bool FlushEnabled
        {
            get => _flushEnabled;
            set { _flushEnabled = value; _setProperties.Add(nameof(FlushEnabled)); }
        }

        /// <summary>
        /// The number of servers that a document will be replicated to.
        /// </summary>
        public int NumReplicas
        {
            get => _numReplicas;
            set { _numReplicas = value; _setProperties.Add(nameof(NumReplicas)); }
        }

        /// <summary>
        /// Whether or not to replicate indexes across the cluster.
        /// </summary>
        public bool ReplicaIndexes
        {
            get => _replicaIndexes;
            set { _replicaIndexes = value; _setProperties.Add(nameof(ReplicaIndexes)); }
        }

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
        public int MaxTtl
        {
            get => _maxTtl;
            set { _maxTtl = value; _setProperties.Add(nameof(MaxTtl)); }
        }

        /// <summary>
        /// The <see cref="CompressionMode"/> to use.
        /// </summary>
        public CompressionMode? CompressionMode { get; set; }

        /// <summary>
        /// Returns the minimum durability level set for the bucket.
        /// </summary>
        /// <remarks>Note that if the bucket does not support it, and by default, it is set to <see cref="DurabilityLevel.None"/>.</remarks>
        public DurabilityLevel DurabilityMinimumLevel
        {
            get => _durabilityMinimumLevel;
            set { _durabilityMinimumLevel = value; _setProperties.Add(nameof(DurabilityMinimumLevel)); }
        }

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
        /// Checks if a property was explicitly set.
        /// </summary>
        internal bool IsSet(string propertyName) => _setProperties.Contains(propertyName);

        /// <summary>
        /// Validates the settings and creates a list of name value pairs to send to the server as form values.
        /// Only explicitly-set properties are included.
        /// </summary>
        /// <param name="isUpdate">
        /// When true, fields that are either supplied via the request URL (name) or that are
        /// immutable on an existing bucket (bucketType) are omitted from the form body.
        /// </param>
        /// <returns></returns>
        internal IReadOnlyDictionary<string, string> ToFormValues(bool isUpdate = false)
        {
            var settings = this;
            var values = new Dictionary<string, string>();

            // For UPDATE the bucket name is in the request URL, so the form body must not contain it.
            if (!isUpdate)
            {
                values.Add("name", settings.Name);
            }

            // bucketType is immutable on an existing bucket and would be rejected by the server on update.
            if (!isUpdate && IsSet(nameof(BucketType)))
            {
                values.Add("bucketType", settings.BucketType.GetDescription());
            }

            if (IsSet(nameof(RamQuotaMB)))
            {
                values.Add("ramQuotaMB", settings.RamQuotaMB.ToStringInvariant());
            }

            if (IsSet(nameof(FlushEnabled)))
            {
                values.Add("flushEnabled", settings.FlushEnabled ? "1" : "0");
            }

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

            if (IsSet(nameof(NumReplicas)))
            {
                values.Add("replicaNumber", settings.NumReplicas.ToStringInvariant());
            }

            if (IsSet(nameof(ReplicaIndexes)))
            {
                values.Add("replicaIndex", settings.ReplicaIndexes ? "1" : "0");
            }

            if (settings.ConflictResolutionType.HasValue)
            {
                values.Add("conflictResolutionType", settings.ConflictResolutionType.GetDescription());
            }

            if (settings.EvictionPolicy.HasValue)
            {
                if (settings.BucketType == Buckets.BucketType.Couchbase)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.NoEviction ||
                        settings.EvictionPolicy == EvictionPolicyType.NotRecentlyUsed)
                    {
                        throw new InvalidArgumentException(
                            "For a Couchbase bucket, the eviction policy can be valueOnly (which is the default) or fullEviction.");
                    }
                }

                if (settings.BucketType == Buckets.BucketType.Ephemeral)
                {
                    if (settings.EvictionPolicy == EvictionPolicyType.ValueOnly ||
                        settings.EvictionPolicy == EvictionPolicyType.FullEviction)
                    {
                        throw new InvalidArgumentException(
                            "For an Ephemeral bucket, the eviction policy can be noEviction (which is the default) or nruEviction.");
                    }
                }

                if (settings.BucketType == Buckets.BucketType.Memcached)
                {
                    throw new InvalidArgumentException("No eviction policy can be assigned to a Memcached bucket.");
                }

                values.Add("evictionPolicy", settings.EvictionPolicy.GetDescription());
            }

            if (IsSet(nameof(MaxTtl)) && settings.MaxTtl > 0)
            {
                values.Add("maxTTL", settings.MaxTtl.ToStringInvariant());
            }

            if (settings.CompressionMode.HasValue)
            {
                values.Add("compressionMode", settings.CompressionMode.GetDescription());
            }

            if (IsSet(nameof(DurabilityMinimumLevel)) && settings.DurabilityMinimumLevel != DurabilityLevel.None)
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
