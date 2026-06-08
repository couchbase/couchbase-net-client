using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Client.Transactions.Internal.Test;
using Microsoft.Extensions.Logging;

namespace Couchbase.Client.Transactions.Cleanup
{
    internal class CleanupWorkQueue : IDisposable
    {
        // CBD-3677
        public const int MaxCleanupQueueDepth = 10_000;
        private readonly CancellationTokenSource _forceFlush = new CancellationTokenSource();

        // A bounded Channel rather than a BlockingCollection: the consumer awaits items via
        // ReadAllAsync, so an idle queue parks no thread-pool thread (NCBC-4218).
        // SingleReader is false: the ConsumeWork loop is the usual reader, but DisposeAsync drains
        // RemainingCleanupRequests concurrently, so more than one reader can be active during shutdown.
        private readonly Channel<CleanupRequest> _workQueue = Channel.CreateBounded<CleanupRequest>(
            new BoundedChannelOptions(MaxCleanupQueueDepth)
            {
                SingleReader = false,
                AllowSynchronousContinuations = false,
            });
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

        public int QueueLength => _workQueue.Reader.Count;

        public CleanupWorkQueue(ICluster cluster, TimeSpan? keyValueTimeout, ILoggerFactory loggerFactory, bool runCleanup)
        {
            _logger = loggerFactory.CreateLogger<CleanupWorkQueue>();
            _cleaner = new Cleaner(cluster, keyValueTimeout, loggerFactory, creatorName: nameof(CleanupWorkQueue)) { TestHooks = TestHooks };
            _consumer = runCleanup ? Task.Run(ConsumeWork) : Task.CompletedTask;
        }

        public IEnumerable<CleanupRequest> RemainingCleanupRequests
        {
            get
            {
                var remaining = new List<CleanupRequest>();
                while (_workQueue.Reader.TryRead(out var cleanupRequest))
                {
                    remaining.Add(cleanupRequest);
                }

                return remaining;
            }
        }

        internal bool TryAddCleanupRequest(CleanupRequest cleanupRequest) => _workQueue.Writer.TryWrite(cleanupRequest);

        private async Task ConsumeWork()
        {
            _logger.LogDebug("{bg} Beginning background cleanup loop.", nameof(CleanupWorkQueue));

            // Initial, naive implementation.
            // Single-threaded consumer that assumes cleanupRequests are in order of transaction expiry already.
            // ReadAllAsync awaits asynchronously when the queue is empty, so no thread-pool thread is held.
            try
            {
                await foreach (var cleanupRequest in _workQueue.Reader.ReadAllAsync(_forceFlush.Token).ConfigureAwait(false))
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
            }
            catch (OperationCanceledException)
            {
                // Expected when ForceFlushAsync/Dispose cancels the loop.
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
                _workQueue.Writer.TryComplete();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            StopProcessing();
            _forceFlush.Dispose();
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
