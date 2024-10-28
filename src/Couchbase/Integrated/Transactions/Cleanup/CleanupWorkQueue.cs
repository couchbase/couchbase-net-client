#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Integrated.Transactions.Internal.Test;
using Microsoft.Extensions.Logging;

namespace Couchbase.Integrated.Transactions.Cleanup
{
    internal class CleanupWorkQueue : IDisposable
    {
        // CBD-3677
        public const int MaxCleanupQueueDepth = 10_000;
        private readonly CancellationTokenSource _forceFlush = new CancellationTokenSource();

        private Channel<CleanupRequest> _workQueue =
            Channel.CreateBounded<CleanupRequest>(MaxCleanupQueueDepth);
        private readonly ILogger<CleanupWorkQueue> _logger;
        private readonly Cleaner _cleaner;

        private TestHookMap _testHooks = new();
        public TestHookMap TestHooks
        {
            get => _testHooks;
            set
            {
                _testHooks = value;
                _cleaner.TestHooks = value;
            }
        }

        public CleanupWorkQueue(ICluster cluster, ILoggerFactory loggerFactory, bool runCleanup)
        {
            _logger = loggerFactory.CreateLogger<CleanupWorkQueue>();
            _cleaner = new Cleaner(cluster, loggerFactory, creatorName: nameof(CleanupWorkQueue)) { TestHooks = TestHooks };
            using (ExecutionContext.SuppressFlow())
            {
                _ = ConsumeWork(_workQueue.Reader);
            }
        }


        internal bool TryAddCleanupRequest(CleanupRequest cleanupRequest) => _workQueue.Writer.TryWrite(cleanupRequest);

        private async ValueTask ConsumeWork(ChannelReader<CleanupRequest> reader)
        {
            _logger.LogDebug("{bg} Beginning background cleanup loop.", nameof(CleanupWorkQueue));

            while (await reader.WaitToReadAsync(_forceFlush.Token).CAF())
            {
                if (reader.TryRead(out var cleanupRequest))
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

            _logger.LogDebug("{bg} Exiting background cleanup loop.", nameof(CleanupWorkQueue));
        }

        /// <summary>
        /// Call during app shutdown to finish all cleanup request as soon as possible.
        /// </summary>
        internal void StopProcessing()
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







