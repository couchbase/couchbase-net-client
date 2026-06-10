#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.Components;
using Couchbase.Client.Transactions.DataModel;

namespace Couchbase.Client.Transactions.DataAccess
{
    internal abstract class CleanerRepositoryBase
    {
        private readonly ICluster _cluster;
        private ICouchbaseCollection? _collection;

        // Names come from the Keyspace, so they need no resolve.
        public Keyspace Keyspace { get; }
        public string BucketName => Keyspace.BucketName;
        public string ScopeName => Keyspace.ScopeName;
        public string CollectionName => Keyspace.CollectionName;

        // 'resolved' optionally seeds the cache when the caller already holds a live collection.
        protected CleanerRepositoryBase(Keyspace keyspace, ICluster cluster, ICouchbaseCollection? resolved = null)
        {
            Keyspace = keyspace ?? throw new ArgumentNullException(nameof(keyspace));
            _cluster = cluster ?? throw new ArgumentNullException(nameof(cluster));
            _collection = resolved;
        }

        /// <summary>
        /// Resolves the keyspace into a usable collection, caching the result on success. Resolution is
        /// deferred to the cleanup loop and only cached when it succeeds, so a transient failure (e.g. a
        /// bucket still warming up) is naturally retried on the next cleanup window.
        /// </summary>
        public async Task<ICouchbaseCollection> GetCollectionAsync(CancellationToken cancellationToken = default)
        {
            var cached = _collection;
            if (cached != null)
            {
                return cached;
            }

            // Cache only on success: a faulted resolve never reaches the assignment below, so _collection
            // stays null and we retry next window. (A Lazy<Task<T>> would cache the faulted task instead,
            // re-introducing the drop-on-failure bug this change fixes.) CompareExchange keeps the first
            // winner if two callers race - redundant resolves are idempotent and need no lock.
            cancellationToken.ThrowIfCancellationRequested();
            var resolved = await Keyspace.ToCouchbaseCollection(_cluster, cancellationToken).ConfigureAwait(false);
            return Interlocked.CompareExchange(ref _collection, resolved, null) ?? resolved;
        }

        public abstract Task<(ClientRecordsIndex? clientRecord, ParsedHLC? parsedHlc, ulong? cas)> GetClientRecord(CancellationToken cancellationToken = default);
        public abstract Task CreatePlaceholderClientRecord(ulong? cas = null, CancellationToken cancellationToken = default);
        public abstract Task RemoveClient(string clientUuid, DurabilityLevel durability = DurabilityLevel.None, CancellationToken cancellationToken = default);
        public abstract Task UpdateClientRecord(string clientUuid, TimeSpan cleanupWindow, int numAtrs, IReadOnlyList<string> expiredClientIds, CancellationToken cancellationToken = default);

        public abstract Task<(Dictionary<string, AtrEntry> attempts, ParsedHLC? parsedHlc)> LookupAttempts(string atrId, CancellationToken cancellationToken = default);
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
