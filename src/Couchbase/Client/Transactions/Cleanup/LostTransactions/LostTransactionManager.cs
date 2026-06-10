#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Internal.Test;
using Couchbase.Client.Transactions.Support;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    internal interface ILostTransactionManager : IAsyncDisposable
    {
        // TODO: When we implement ExtCustomMetadataCollection, we'll need an overload that handles that
        Task StartAsync(CancellationToken token);
    }

    internal class LostTransactionManager : IAsyncDisposable
    {
        private readonly ILogger<LostTransactionManager> _logger;
        private readonly ILoggerFactory _loggerFactory;

        // Why Lazy<T> and not just T?  Turns out, we _never_ want our function which creates a
        // PerCollectionCleaner to be called more than once for the same key.  GetOrAdd actually calls the
        // generating function for the value under contention when it doesn't actually end up doing an insert
        // because someone else got there first.   The Lazy<T> insures the actual initialization of the lazy is
        // only done once.   You can see tests fail when this isn't the case.
        private static readonly ConcurrentDictionary<Keyspace, Lazy<PerCollectionCleaner>>
            CollectionsToClean = new();

        private readonly ICluster _cluster;
        private readonly TimeSpan _cleanupWindow;
        private readonly TimeSpan? _keyValueTimeout;
        private readonly CancellationTokenSource _overallCancellation = new();

        public string ClientUuid { get; }
        public ICleanupTestHooks TestHooks { get; set; } = DefaultCleanupTestHooks.Instance;
        public List<Keyspace> CollectionsBeingCleaned => CollectionsToClean.Keys.ToList();

        public void AddCollection(ICouchbaseCollection collection)
        {
            // we need to add _only_ if the collection isn't already being cleaned...
            _logger.LogInformation($"AddCollection called, currently cleaning {CollectionsToClean.Count} collections");
            // We already hold a live collection here, so seed the cache to avoid re-resolving.
            // Force the lazy's Value so the cleaner is actually created.
            var keyspace = new Keyspace(collection);
            _ = CollectionsToClean.GetOrAdd(keyspace,
                ks => new Lazy<PerCollectionCleaner>(() => CleanerForCollection(ks, startDisabled: false, resolved: collection))).Value;
        }

        // Register a configured collection by keyspace; the collection resolves lazily on the cleaner's
        // loop. Synchronous — no resolution, no blocking.
        private void RegisterCollection(Keyspace keyspace)
        {
            _ = CollectionsToClean.GetOrAdd(keyspace,
                ks => new Lazy<PerCollectionCleaner>(() => CleanerForCollection(ks, startDisabled: false))).Value;
        }

        internal LostTransactionManager(ICluster cluster, ILoggerFactory loggerFactory, TimeSpan cleanupWindow, TimeSpan? keyValueTimeout, string? clientUuid = null, bool startDisabled = false,  List<Keyspace>? collections = null)
        {
            ClientUuid = clientUuid ?? Guid.NewGuid().ToString();
            _logger = loggerFactory.CreateLogger<LostTransactionManager>();
            _loggerFactory = loggerFactory;
            _cluster = cluster;
            _cleanupWindow = cleanupWindow;
            _keyValueTimeout = keyValueTimeout;
            _logger.LogDebug("Starting LostTransactionManager");

            // No configured collections: nothing to register up front (the common no-transactions case).
            if (collections is null or { Count: 0 }) return;

            // Register configured collections synchronously by keyspace; each resolves lazily on its
            // cleaner's loop (no blocking).
            foreach (var keyspace in collections)
            {
                _logger.LogDebug("Registering configured cleanup collection {keyspace}", keyspace);
                RegisterCollection(keyspace);
            }
            _logger.LogDebug($"LostTransactionManager {ClientUuid} started with {CollectionsToClean.Count} collections to be cleaned");
        }

        public void Start()
        {

        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogDebug("LostTransactionManager being disposed");
            _overallCancellation.Cancel();
            await RemoveClientEntries().CAF();
            _logger.LogDebug("LostTransactionManager disposed");
        }

        private async Task RemoveClientEntries()
        {

            while (!CollectionsToClean.IsEmpty)
            {
                var buckets = CollectionsToClean.ToArray();
                List<ValueTask> disposeTasks = new();
                // populate disposeTasks with all the DisposeAsync tasks first.
                foreach (var bkt in buckets)
                {
                    _logger.LogDebug("Shutting down cleaner for '{bkt}", bkt.Value);
                    disposeTasks.Add(bkt.Value.Value.DisposeAsync());
                }
                try
                {
                    // now wait for them to all be done....
                    _logger.LogDebug($"Waiting for {disposeTasks.Count} PerCollectionCleaner tasks to complete");
                    await Task.WhenAll(disposeTasks.Select(vt => vt.AsTask())).CAF();
                    _logger.LogDebug("All cleanup tasks completed");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error while shutting down lost transaction cleanup : {ex}", ex);
                }
                finally
                {
                    // now remove them from the CollectionsToClean Dictionary.
                    foreach (var bkt in buckets)
                    {
                        var success = CollectionsToClean.TryRemove(bkt.Key, out _);
                        _logger.LogDebug(
                            "Removed cleaner '{bkt}' from collections to clean {status}", bkt.Value,
                            success ? "successfully" : "unsuccessfully");
                    }
                }
            }
            _logger.LogDebug("Client entries all removed.");
        }

        private PerCollectionCleaner CleanerForCollection(Keyspace keyspace, bool startDisabled, ICouchbaseCollection? resolved = null)
        {
            _logger.LogDebug("New cleaner for {collection}", keyspace);
            var repository = new CleanerRepository(keyspace, _cluster, _keyValueTimeout, resolved);
            var cleaner = new Cleaner(_cluster, _keyValueTimeout, _loggerFactory, creatorName: nameof(LostTransactionManager));
            return new PerCollectionCleaner(ClientUuid, cleaner, repository, _cleanupWindow, _loggerFactory, startDisabled, onCollectionNotFound: RemoveFromCleanupSet) { TestHooks = TestHooks };
        }

        // Invoked by a PerCollectionCleaner when the server reports its collection as not found; drops the
        // dictionary entry (the cleaner stops its own timer).
        private void RemoveFromCleanupSet(Keyspace keyspace)
        {
            if (CollectionsToClean.TryRemove(keyspace, out _))
            {
                _logger.LogInformation("Removed collection {keyspace} from the cleanup set (not found)", keyspace);
            }
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
