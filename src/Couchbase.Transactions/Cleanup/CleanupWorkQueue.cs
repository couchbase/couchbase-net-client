using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Transcoders;
using Couchbase.KeyValue;
using Couchbase.Transactions.Components;
using Couchbase.Transactions.Error;
using Couchbase.Transactions.Error.External;
using Couchbase.Transactions.Internal.Test;
using Couchbase.Transactions.Support;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Transactions.Cleanup
{
    internal class CleanupWorkQueue : IDisposable
    {
        // CBD-3677
        public const int MaxCleanupQueueDepth = 10_000;
        private readonly CancellationTokenSource _forceFlush = new CancellationTokenSource();

        // TODO: This needs to be bounded, with a circuit breaker.
        private readonly BlockingCollection<CleanupRequest> _workQueue = new BlockingCollection<CleanupRequest>(MaxCleanupQueueDepth);
        private readonly Task _consumer;
        private readonly ILogger<CleanupWorkQueue> _logger;
        private readonly Cleaner _cleaner;

        private ICleanupTestHooks _testHooks = DefaultCleanupTestHooks.Instance;
        public ICleanupTestHooks TestHooks
        {
            get => _testHooks;
            set
            {
                _testHooks = value;
                _cleaner.TestHooks = value;
            }
        }

        public int QueueLength => _workQueue.Count;

        public CleanupWorkQueue(ICluster cluster, TimeSpan? keyValueTimeout, ILoggerFactory loggerFactory, bool runCleanup)
        {
            _logger = loggerFactory.CreateLogger<CleanupWorkQueue>();
            _cleaner = new Cleaner(cluster, keyValueTimeout, loggerFactory, creatorName: nameof(CleanupWorkQueue)) { TestHooks = TestHooks };
            _consumer = runCleanup ? Task.Run(ConsumeWork) : Task.CompletedTask;
        }

        public IEnumerable<CleanupRequest> RemainingCleanupRequests => _workQueue.ToArray();

        internal bool TryAddCleanupRequest(CleanupRequest cleanupRequest) => _workQueue.TryAdd(cleanupRequest);

        private async Task ConsumeWork()
        {
            _logger.LogDebug("{bg} Beginning background cleanup loop.", nameof(CleanupWorkQueue));

            // Initial, naive implementation.
            // Single-threaded consumer that assumes cleanupRequests are in order of transaction expiry already
            foreach (var cleanupRequest in _workQueue.GetConsumingEnumerable(_forceFlush.Token))
            {
                var delay = cleanupRequest.WhenReadyToBeProcessed - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero && !_forceFlush.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(delay, _forceFlush.Token).CAF();
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }

                try
                {
                    var cleanupResult = await _cleaner.ProcessCleanupRequest(cleanupRequest).ConfigureAwait(false);
                    if (!cleanupResult.Success && cleanupResult.FailureReason != null)
                    {
                        throw new CleanupFailedException(cleanupResult.FailureReason);
                    }
                }
                catch (Exception ex)
                {
                    // EXT_REMOVE_COMPLETED: Leave it for the lost cleanup process;  No retry.
                    cleanupRequest.ProcessingErrors.Enqueue(ex);
                }
            }

            _logger.LogDebug("{bg} Exiting background cleanup loop.", nameof(CleanupWorkQueue));
        }

        /// <summary>
        /// Call during app shutdown to finish all cleanup request as soon as possible.
        /// </summary>
        /// <returns>A Task representing asynchronous work.</returns>
        internal async Task ForceFlushAsync()
        {
            StopProcessing();
            await _consumer.CAF();
        }

        private void StopProcessing()
        {
            try
            {
                _forceFlush.Cancel(throwOnFirstException: false);
                _workQueue.CompleteAdding();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            StopProcessing();
            _workQueue.Dispose();
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
