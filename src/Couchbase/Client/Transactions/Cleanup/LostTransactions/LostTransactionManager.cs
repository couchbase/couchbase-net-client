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
            // We have to get the value of the lazy, and assign to a variable (even though unused) to insure the lazy actually gets instantiated
            var perCollectionCleanerWeDontUse = CollectionsToClean.GetOrAdd(new Keyspace(collection), _ => new Lazy<PerCollectionCleaner> (() => CleanerForCollection(collection, false))).Value;
        }

        private async Task AddCollection(Keyspace? collection = null)
        {
            if (collection != null)
            {
                try
                {
                    AddCollection(await collection.ToCouchbaseCollection(_cluster).CAF());
                } catch (CouchbaseException e) {
                    // that's just fine - we couldn't get the collection so let's just proceed without doing so
                    _logger.LogError(e, "Couldn't set the metadataCollection");
                }
            }
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

            if (collections == null) return;
            Task.Run(async () =>
            {
                var tasks = new List<Task>();
                collections.ForEach(collection =>
                {
                    _logger.LogDebug($"Starting cleanup of metadata collection {collection}");
                    tasks.Add(AddCollection(collection));
                });
                await Task.WhenAll(tasks.ToArray()).CAF();
            }).GetAwaiter().GetResult();
            _logger.LogDebug($"LostTransactionManager {ClientUuid} started with ${CollectionsToClean.Count} collections to be cleaned");

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

        private PerCollectionCleaner CleanerForCollection(ICouchbaseCollection collection, bool startDisabled)
        {
            _logger.LogDebug("New cleaner for {collection}", collection.MakeKeyspace());
            var repository = new CleanerRepository(collection, _keyValueTimeout);
            var cleaner = new Cleaner(_cluster, _keyValueTimeout, _loggerFactory, creatorName: nameof(LostTransactionManager));
            return new PerCollectionCleaner(ClientUuid, cleaner, repository, _cleanupWindow, _loggerFactory, startDisabled) { TestHooks = TestHooks };
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
