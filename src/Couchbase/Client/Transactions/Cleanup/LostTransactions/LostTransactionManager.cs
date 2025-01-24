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
        private const int DiscoverBucketsPeriodMs = 10_000;
        private readonly ILogger<LostTransactionManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, PerBucketCleaner> _discoveredBuckets = new ConcurrentDictionary<string, PerBucketCleaner>();
        private readonly ICluster _cluster;
        private readonly TimeSpan _cleanupWindow;
        private readonly TimeSpan? _keyValueTimeout;
        private readonly Timer _discoverBucketsTimer;
        private readonly CancellationTokenSource _overallCancellation = new CancellationTokenSource();
        private readonly SemaphoreSlim _timerCallbackMutex = new SemaphoreSlim(1);

        public string ClientUuid { get; }
        public ICleanupTestHooks TestHooks { get; set; } = DefaultCleanupTestHooks.Instance;
        public int DiscoveredBucketCount => _discoveredBuckets.Count;
        public int RunningCount => _discoveredBuckets.Where(pbc => pbc.Value.Running).Count();
        public long TotalRunCount => _discoveredBuckets.Sum(pbc => pbc.Value.RunCount);

        internal LostTransactionManager(ICluster cluster, ILoggerFactory loggerFactory, TimeSpan cleanupWindow, TimeSpan? keyValueTimeout, string? clientUuid = null, bool startDisabled = false, ICouchbaseCollection? metadataCollection = null)
        {
            ClientUuid = clientUuid ?? Guid.NewGuid().ToString();
            _logger = loggerFactory.CreateLogger<LostTransactionManager>();
            _loggerFactory = loggerFactory;
            _cluster = cluster;
            _cleanupWindow = cleanupWindow;
            _keyValueTimeout = keyValueTimeout;

            if (metadataCollection == null)
            {
                // discover all buckets periodically and make sure there is a 1:1 relationship between cleaners and buckets
                _discoverBucketsTimer = new Timer(DiscoverAndCleanAllBucketsCallback, null, startDisabled ? -1 : 0, DiscoverBucketsPeriodMs);
            }
            else
            {
                // cleanup only the specifified collection.
                var singleBucketCleaner = CleanerForCollection(metadataCollection, startDisabled);
                _discoveredBuckets.TryAdd(metadataCollection.Scope.Bucket.Name, singleBucketCleaner);
                _discoverBucketsTimer = new Timer(PlaceholderDoNothingCallback, null, -1, DiscoverBucketsPeriodMs);
            }
        }

        public void Start()
        {
            _discoverBucketsTimer.Change(0, DiscoverBucketsPeriodMs);
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogDebug("Shutting down.");
            _discoverBucketsTimer.Change(-1, DiscoverBucketsPeriodMs);
            _overallCancellation.Cancel();
            await RemoveClientEntries().CAF();
            // since we target netstandard 2.0, Timer has no DisposeAsync
            await Task.Run(()  => _discoverBucketsTimer.Dispose()).CAF();
        }

        private async Task RemoveClientEntries()
        {
            try
            {
                await _timerCallbackMutex.WaitAsync().CAF();
                while (!_discoveredBuckets.IsEmpty)
                {
                    var buckets = _discoveredBuckets.ToArray();
                    foreach (var bkt in buckets)
                    {
                        try
                        {
                            _logger.LogDebug("Shutting down cleaner for '{bkt}", bkt.Value);
                            await bkt.Value.DisposeAsync().CAF();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Error while shutting down lost transaction cleanup for '{bkt}': {ex}", bkt.Value, ex);
                        }
                        finally
                        {
                            _discoveredBuckets.TryRemove(bkt.Key, out _);
                        }
                    }
                }
            }
            finally
            {
                _timerCallbackMutex.Release();
            }
        }

        private async void DiscoverAndCleanAllBucketsCallback(object? state)
        {
            try
            {
                await DiscoverBuckets(startDisabled: false).CAF();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Bucket discovery failed: {ex}", ex);
            }
        }

        private void PlaceholderDoNothingCallback(object? state)
        { }

        public async Task DiscoverBuckets(bool startDisabled, Func<ICleanupTestHooks>? setupTestHooks = null)
        {
            try
            {
                var passed = await _timerCallbackMutex.WaitAsync(DiscoverBucketsPeriodMs, _overallCancellation.Token).CAF();
                if (!passed)
                {
                    // stopped waiting due to cancellation
                    return;
                }

                foreach (var existingBucket in _discoveredBuckets.ToArray())
                {
                    if (!existingBucket.Value.Running
                        && _discoveredBuckets.TryRemove(existingBucket.Key, out var removedBucket))
                    {
                        _logger.LogInformation("Cleaner for bucket '{bkt}' was  not running and was removed.", removedBucket.FullBucketName);
                    }
                }

                Dictionary<string, Management.Buckets.BucketSettings> buckets;
                try
                {
                    try
                    {
                        buckets = await _cluster.Buckets.GetAllBucketsAsync().CAF();
                    }
                    catch (ArgumentNullException)
                    {
                        _logger.LogWarning("GetAllBuckets failed due to ArgumentNullException.  Cluster not ready?");
                        await _cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(30)).CAF();
                        _logger.LogInformation("Cluster ready.  Retrying GetAllBuckets...");
                        buckets = await _cluster.Buckets.GetAllBucketsAsync().CAF();
                    }
                    catch (NullReferenceException)
                    {
                        _logger.LogWarning("GetAllBuckets failed due to NullReferenceException.  Cluster not ready?");
                        await _cluster.WaitUntilReadyAsync(TimeSpan.FromSeconds(30)).CAF();
                        _logger.LogInformation("Cluster ready.  Retrying GetAllBuckets...");
                        buckets = await _cluster.Buckets.GetAllBucketsAsync().CAF();
                    }
                }
                catch (ArgumentNullException)
                {
                    _logger.LogWarning("GetAllBuckets failed due to Cluster still not ready");
                    return;
                }
                catch (NullReferenceException)
                {
                    _logger.LogWarning("GetAllBuckets failed due to Cluster still not ready");
                    return;
                }

                foreach (var bkt in buckets)
                {
                    var bucketName = bkt.Key;
                    _logger.LogDebug("Discovered {bkt}", bucketName);
                    if (!_discoveredBuckets.TryGetValue(bucketName, out var existingCleaner))
                    {
                        var newCleaner = await CleanerForBucket(bucketName, startDisabled).CAF();
                        setupTestHooks ??= () => TestHooks;
                        newCleaner.TestHooks = setupTestHooks() ?? newCleaner.TestHooks;
                        _logger.LogDebug("New Bucket Cleaner: {cleaner}", newCleaner);
                        _discoveredBuckets.TryAdd(bucketName, newCleaner);
                    }
                    else
                    {
                        _logger.LogDebug("Existing Bucket Cleaner: {cleaner}", existingCleaner.FullBucketName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Discover bucket failed: {ex}", ex);
            }
            finally
            {
                _timerCallbackMutex.Release();
            }
        }

        private async Task<PerBucketCleaner> CleanerForBucket(string bucketName, bool startDisabled)
        {
            var bucket = await _cluster.BucketAsync(bucketName).CAF();
            var collection = bucket.DefaultCollection();
            return CleanerForCollection(collection, startDisabled);
        }

        private PerBucketCleaner CleanerForCollection(ICouchbaseCollection collection, bool startDisabled)
        {
            _logger.LogDebug("New cleaner for {collection}", collection.MakeKeyspace());
            var repository = new CleanerRepository(collection, _keyValueTimeout);
            var cleaner = new Cleaner(_cluster, _keyValueTimeout, _loggerFactory, creatorName: nameof(LostTransactionManager));
            return new PerBucketCleaner(ClientUuid, cleaner, repository, _cleanupWindow, _loggerFactory, startDisabled) { TestHooks = TestHooks };
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
