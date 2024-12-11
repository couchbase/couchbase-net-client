#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Config;
using Couchbase.Client.Transactions.DataAccess;
using Couchbase.Client.Transactions.Internal.Test;
using Couchbase.Client.Transactions.Support;
using Couchbase.KeyValue;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Cleanup.LostTransactions
{
    internal class LostTransactionManager : IAsyncDisposable, IDisposable
    {
        private const int DiscoverBucketsPeriodMs = 10_000;
        private readonly ILogger<LostTransactionManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ICluster _cluster;
        private readonly TimeSpan _cleanupWindow;
        private readonly CancellationTokenSource _overallCancellation = new CancellationTokenSource();
        private readonly ConcurrentDictionary<KeySpace, PerCollectionCleaner> _collectionsToClean = new();

        public string ClientUuid { get; }
        public TestHookMap TestHooks { get; set; } = new();

        public int RunningCount => _collectionsToClean.Where(pbc => pbc.Value.Running).Count();
        public long TotalRunCount => _collectionsToClean.Sum(pbc => pbc.Value.RunCount);

        /// <summary>
        /// The set of collections currently being monitored for cleaning.
        /// </summary>
        /// <remarks>
        /// The name "cleanupSet" is specified in the RFC, despite the underlying collection being a dictionary.
        /// </remarks>
        public IEnumerable<KeySpace> CleanupSet => _collectionsToClean.Keys;

        internal LostTransactionManager(ICluster cluster, ILoggerFactory loggerFactory, TransactionCleanupConfig cleanupConfig, string? clientUuid = null, KeySpace? metadataCollection = null)
        {
            ClientUuid = clientUuid ?? Guid.NewGuid().ToString();
            _logger = loggerFactory.CreateLogger<LostTransactionManager>();
            _loggerFactory = loggerFactory;
            _cluster = cluster;
            _cleanupWindow = cleanupConfig.CleanupWindow ?? TransactionConfig.DefaultCleanupWindow;
            TrackCollectionForCleanup(metadataCollection);
            foreach (var ks in cleanupConfig.Collections ?? [])
            {
                TrackCollectionForCleanup(ks);
            }
        }

        public void TrackCollectionForCleanup(KeySpace? collection)
        {
            if (collection is not null)
            {
                _ = _collectionsToClean.GetOrAdd(collection, _ =>
                    CleanerForCollection(collection, startDisabled: false, shutdownToken: _overallCancellation.Token)
                );
            }
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogDebug("{this}: Shutting down.", nameof(LostTransactionManager));
            _overallCancellation.Cancel();
            await RemoveClientEntries().CAF();
        }

        private async Task RemoveClientEntries()
        {
            while (!_collectionsToClean.IsEmpty)
            {
                var toRemove = _collectionsToClean.Keys.ToList();
                foreach (var ks in toRemove)
                {
                    if (_collectionsToClean.TryRemove(ks, out var cleaner))
                    {
                        try
                        {
                            await cleaner.DisposeAsync().CAF();
                        }
                        catch (Exception e)
                        {
                            _logger.LogError(e, "Cleaner shutdown failed for " + cleaner.FullClientName);
                        }
                    }
                }
            }
        }

        private PerCollectionCleaner CleanerForCollection(KeySpace collection, bool startDisabled,
            CancellationToken shutdownToken)
        {
            _logger.LogDebug("New cleaner for {collection}", collection);
            var repository = new CleanerRepository(collection, _cluster);
            var cleaner = new Cleaner(_cluster, _loggerFactory, creatorName: nameof(LostTransactionManager));
            return new PerCollectionCleaner(ClientUuid, cleaner, repository, _cleanupWindow, _loggerFactory,
                shutdownToken, startDisabled) { TestHooks = TestHooks };
        }

        public void Dispose()
        {
            if (_overallCancellation.IsCancellationRequested)
            {
                // already disposed
                return;
            }

            _logger.LogDebug("{this}: Shutting down synchronously.", nameof(LostTransactionManager));
            _overallCancellation.Cancel();
            foreach (var toClean in _collectionsToClean.Values.ToList())
            {
                toClean.Dispose();
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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







