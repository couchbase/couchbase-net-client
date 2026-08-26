using System;
using Couchbase.Core;
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions.Support
{
    internal static class CollectionExtensions
    {
        /// <summary>
        /// Whether the cluster behind this collection can preserve expiry on a mutation.
        /// </summary>
        /// <remarks>
        /// This used to be <c>Bucket.SupportsCollections</c>, with a comment calling it "a proxy for
        /// supporting TTLs". They are unrelated capabilities: every node in a mixed-version cluster
        /// can support collections while some node has not negotiated PreserveTtl, and
        /// CouchbaseCollection throws FeatureNotAvailableException for exactly that combination - so
        /// the proxy asked transactions to request something the cluster would then refuse.
        /// Protostellar has no ClusterContext to ask, so it keeps the old proxy.
        /// </remarks>
        public static bool SupportsPreserveTtl(this ICouchbaseCollection collection) =>
            collection.Scope.Bucket is BucketBase bucket
                ? bucket.Context.SupportsPreserveTtl
                : collection.Scope.Bucket.SupportsCollections;

        public static RemoveOptions Timeout(this RemoveOptions opts, TimeSpan? timeout)
        {
            if (timeout.HasValue)
            {
                return opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static MutateInOptions Timeout(this MutateInOptions opts, TimeSpan? timeout)
        {
            if (timeout.HasValue)
            {
                return opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static LookupInOptions Timeout(this LookupInOptions opts, TimeSpan? timeout)
        {
            if (timeout.HasValue)
            {
                return opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static MutateInOptions Durability(this MutateInOptions opts, DurabilityLevel? durability)
        {
            if (durability != null)
            {
                return opts.Durability(durability.Value);
            }

            return opts;
        }

        internal static string MakeKeyspace(this ICouchbaseCollection collection) => $"default:`{collection.Scope.Bucket.Name}`.`{collection.Scope.Name}`.`{collection.Name}`";
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
