using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;

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

        /// <summary>
        /// The type of storage to use with the bucket. This is only specified for "couchbase" buckets.
        /// </summary>
        [InterfaceStability(Level.Uncommitted)]
        public StorageBackend? StorageBackend { get; set; }

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
                {"ramQuotaMB", settings.RamQuotaMB.ToString()},
                {"flushEnabled", settings.FlushEnabled ? "1" : "0"}
            };

            if (settings.BucketType != BucketType.Memcached)
            {
                values.Add("replicaNumber", settings.NumReplicas.ToString());
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
                values.Add("maxTTL", settings.MaxTtl.ToString());
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

            return values;
        }

        internal static BucketSettings FromJson(JToken json)
        {
            var settings = new BucketSettings
            {
                Name = json.GetTokenValue<string>("name"),
                MaxTtl = json.GetTokenValue<int>("maxTTL"),
                RamQuotaMB = json.GetTokenValue<long>("quota.rawRAM"),
                FlushEnabled = json.SelectToken("controllers.flush") != null
            };

            var bucketTypeToken = json.SelectToken("bucketType");
            if (bucketTypeToken != null &&
                EnumExtensions.TryGetFromDescription(bucketTypeToken.Value<string>(), out BucketType bucketType))
            {
                settings.BucketType = bucketType;
            }

            if (settings.BucketType != BucketType.Memcached)
            {
                settings.NumReplicas = json.GetTokenValue<int>("replicaNumber");
            }

            if (settings.BucketType == BucketType.Couchbase)
            {
                settings.ReplicaIndexes = json.GetTokenValue<bool>("replicaIndex");
            }

            var conflictResolutionToken = json.SelectToken("conflictResolutionType");
            if (conflictResolutionToken != null &&
                EnumExtensions.TryGetFromDescription(conflictResolutionToken.Value<string>(), out ConflictResolutionType conflictResolutionType))
            {
                settings.ConflictResolutionType = conflictResolutionType;
            }

            var compressionModeToken = json.SelectToken("compressionMode");
            if (compressionModeToken != null &&
                EnumExtensions.TryGetFromDescription(compressionModeToken.Value<string>(), out CompressionMode compressionMode))
            {
                settings.CompressionMode = compressionMode;
            }

            var evictionPolicyToken = json.SelectToken("evictionPolicy");
            if (evictionPolicyToken != null &&
                EnumExtensions.TryGetFromDescription(evictionPolicyToken.Value<string>(), out EvictionPolicyType evictionPolicyType))
            {
                settings.EvictionPolicy = evictionPolicyType;
            }

            var durabilityMinLevelToken = json.SelectToken("durabilityMinLevel");
            if (durabilityMinLevelToken != null &&
                EnumExtensions.TryGetFromDescription(durabilityMinLevelToken.Value<string>(),
                    out DurabilityLevel durabilityMinLevel))
            {
                settings.DurabilityMinimumLevel = durabilityMinLevel;
            }

            var storageBackend = json.SelectToken("storageBackend");
            if (storageBackend != null &&
                EnumExtensions.TryGetFromDescription(storageBackend.Value<string>(), out StorageBackend storageBackendType))
            {
                settings.StorageBackend = storageBackendType;
            }

            return settings;
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
