using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Exception = System.Exception;

#nullable enable

namespace Couchbase.Core.IO.Connections.DataFlow
{
    /// <summary>
    /// Connection pool based on queuing operations via the TPL data flows library.
    /// </summary>
    internal class DataFlowConnectionPool : ConnectionPoolBase
    {
        private readonly IConnectionPoolScaleController _scaleController;
        private readonly IRedactor _redactor;
        private readonly ILogger<DataFlowConnectionPool> _logger;
        private readonly uint _kvSendQueueCapacity;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1);
        private readonly List<(IConnection Connection, ActionBlock<QueueItem> Block)> _connections =
            new List<(IConnection Connection, ActionBlock<QueueItem> Block)>();

        private readonly BufferBlock<QueueItem> _sendQueue;

        private bool _initialized;

        /// <inheritdoc />
        public sealed override int Size => _connections.Count;

        /// <inheritdoc />
        public sealed override int MinimumSize { get; set; }

        /// <inheritdoc />
        public sealed override int MaximumSize { get; set; }

        /// <inheritdoc />
        public sealed override int PendingSends => _sendQueue.Count;

        /// <summary>
        /// Creates a new DataFlowConnectionPool.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="scaleController">Scale controller.</param>
        /// <param name="redactor">Log redactor.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="kvSendQueueCapacity"></param>
        public DataFlowConnectionPool(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory,
            IConnectionPoolScaleController scaleController, IRedactor redactor, ILogger<DataFlowConnectionPool> logger, uint kvSendQueueCapacity)
            : base(connectionInitializer, connectionFactory)
        {
            _scaleController = scaleController ?? throw new ArgumentNullException(nameof(scaleController));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kvSendQueueCapacity = kvSendQueueCapacity;

            MinimumSize = 2;
            MaximumSize = 5;

           _sendQueue = new BufferBlock<QueueItem>(new DataflowBlockOptions
            {
                BoundedCapacity = (int)_kvSendQueueCapacity
            });

            TrackConnectionPool(this);
        }

        /// <inheritdoc />
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (_initialized)
            {
                return;
            }

            await AddConnectionsAsync(MinimumSize, cancellationToken).ConfigureAwait(false);

            _scaleController.Start(this);

            _logger.LogDebug("Connection pool for {endpoint} initialized with {size} connections.",
                _redactor.SystemData(EndPoint), MinimumSize);

            _initialized = true;
        }

        /// <inheritdoc />
        public override Task SendAsync(IOperation operation, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            // We don't need the execution context to flow to sends
            // so we can reduce heap allocations by not flowing.
            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                if (Size > 0)
                {
                    if (!_sendQueue.Post(new QueueItem
                        { Operation = operation, CancellationToken = cancellationToken }))
                    {
                        MetricTracker.KeyValue.TrackSendQueueFull();
                        throw new SendQueueFullException();
                    }

                    return Task.CompletedTask;
                }

                // We had all connections die earlier and fail to restart, we need to restart them
                return CleanupDeadConnectionsAsync().ContinueWith(_ =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Requeue the request
                    // Note: always requeues even if cleanup fails
                    // Since the exception on the task is ignored, we're also eating the exception

                    if (!_sendQueue.Post(new QueueItem
                        { Operation = operation, CancellationToken = cancellationToken }))
                    {
                        MetricTracker.KeyValue.TrackSendQueueFull();
                        throw new SendQueueFullException();
                    }
                }, cancellationToken);
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        /// <inheritdoc />
        public override IEnumerable<IConnection> GetConnections()
        {
            EnsureNotDisposed();

            return new List<IConnection>(_connections.Select(p => p.Connection));
        }

        /// <inheritdoc />
        public override async Task ScaleAsync(int delta)
        {
            if (delta > 0)
            {
                var growBy = Math.Min(delta, MaximumSize - Size);
                if (growBy > 0)
                {
                    await AddConnectionsAsync(growBy, _cts.Token).ConfigureAwait(false);
                }
            }
            else if (delta < 0)
            {
                var shrinkBy = Math.Min(-delta, Size - MinimumSize);
                if (shrinkBy > 0)
                {
                    // Select connections to shrink, longest inactive first
                    var toShrink = _connections
                        .OrderByDescending(p => p.Connection.IdleTime)
                        .Select((connection, index) => (index, connection))
                        .Take(shrinkBy)
                        .ToList();

                    // Stop all connections from receiving new sends, and wait for in flight sends
                    // to complete, in parallel
                    var completionTasks = toShrink
                        .Select(p =>
                        {
                            p.connection.Block.Complete();
                            return p.connection.Block.Completion;
                        })
                        .ToList();

                    // Wait for all stops to be done
                    await Task.WhenAll(completionTasks).ConfigureAwait(false);

                    // Dispose and remove from _connections
                    // Do it in reverse order so we can remove by index safely
                    foreach (var p in toShrink.OrderByDescending(p => p.index))
                    {
                        _connections.RemoveAt(p.index);

#pragma warning disable 4014
                        // Don't wait for close, let it happen in the background
                        p.connection.Connection.CloseAsync(TimeSpan.FromMinutes(1));
#pragma warning restore 4014
                    }
                }
            }
        }

        /// <inheritdoc />
        public override async ValueTask<IAsyncDisposable> FreezePoolAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

            return new FreezeDisposable(this);
        }

        private void EnsureNotDisposed()
        {
            if (_cts.IsCancellationRequested)
            {
                //Were not throwing an ODE because we want a more specific exception that reuse the retry logic in the RetryOrchestrator
                ThrowHelper.ThrowSocketNotAvailableException(nameof(DataFlowConnectionPool));
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            _logger.LogDebug("Disposing pool for {endpoint}.", EndPoint);
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            _scaleController.Dispose();
            _cts.Cancel(false);

            // Take out a lock to prevent more connections from opening while we're disposing
            // Don't need to release
            _lock.Wait();
            try
            {
                // Complete any queued commands
                _sendQueue.Complete();

                // Dispose of the connections
                foreach (var connection in _connections)
                {
                    connection.Connection.Dispose();
                }

                _connections.Clear();
            }
            finally
            {
                _lock.Dispose();
                _cts.Dispose();
            }
        }

        /// <summary>
        /// For UNIT TESTING ONLY. Causes all future operations to fail with <see cref="SendQueueFullException"/>.
        /// </summary>
        protected internal void CompleteSendQueue()
        {
            _sendQueue.Complete();
        }

        #region Connection Management

        /// <summary>
        /// Adds a certain number of connections to the pool. Assumes that the pool is already locked.
        /// </summary>
        /// <param name="count">Number of connections to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <remarks>
        /// This method will fail if the total number of requested connections could not be added.
        /// However, it may have partially succeeded, some connections may have been added.
        /// </remarks>
        private async Task AddConnectionsAsync(int count, CancellationToken cancellationToken = default)
        {
            if (count <= 0)
            {
                return;
            }

            async Task StartConnection()
            {
                // Create and initialize a new connection
                var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

                if (connection.IsDead)
                {
                    _logger.LogDebug("Connection for {endpoint} could not be started.", EndPoint);
                    return;
                }
                _logger.LogDebug("Connection for {endpoint} has been started.", EndPoint);

                // Create an ActionBlock to receive messages for this connection
                var block = new ActionBlock<QueueItem>(BuildHandler(connection),
                    new ExecutionDataflowBlockOptions
                    {
                        BoundedCapacity = 1, // Don't let the action block queue up requests, they should queue in the buffer block
                        MaxDegreeOfParallelism = 1, // Each connection can only process one send at a time
                        SingleProducerConstrained = true // Can provide better performance since we know only
                    });

                // Receive messages from the queue
                _sendQueue.LinkTo(block, new DataflowLinkOptions
                {
                    PropagateCompletion = true
                });

                lock (_connections)
                {
                    // As each connection is successful, add it to our list of connections
                    // This way if 4 succeed and 1 fails, the 4 that succeed are still up and available
                    // We need an additional lock here because _connections.Add might get called
                    // simultaneously as each connection is successfully started, but this is a different
                    // lock from the preexisting lock on the overall pool using _lock.

                    _connections.Add((connection, block));
                }
            }

            // Startup connections up to the minimum pool size in parallel
            var tasks =
                Enumerable.Range(1, count)
                    .Select(p => StartConnection())
                    .ToList();

            // Wait for all connections to be started
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a SendOperationRequest handler for a specific connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <returns>The handler.</returns>
        private Func<QueueItem, Task> BuildHandler(IConnection connection)
        {
            return async request =>
            {
                try
                {
                    // ignore request that timed out or was cancelled while in queue
                    if (request.CancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (connection.IsDead)
                    {
                        // We need to return the task from CleanupDeadConnectionsAsync
                        // Because as long as the task is not completed, this connection won't
                        // receive more requests. We need to wait until the dead connection is
                        // unlinked to make sure no more bad requests hit it.
                        await CleanupDeadConnectionsAsync().ConfigureAwait(false);

                        if (!_cts.IsCancellationRequested && !request.CancellationToken.IsCancellationRequested)
                        {
                            // We don't need the execution context to flow to sends
                            // so we can reduce heap allocations by not flowing.
                            using (ExecutionContext.SuppressFlow())
                            {
                                // Requeue the request for a different connection
                                // Note: always requeues even if cleanup fails
                                // Since the exception on the task is ignored, we're also eating the exception
                                if (!_sendQueue.Post(request))
                                {
                                    MetricTracker.KeyValue.TrackSendQueueFull();
                                    throw new SendQueueFullException();
                                }
                            }
                        }
                    }
                    else
                    {
                        await request.Operation.SendAsync(connection, request.CancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Cancellation during send shouldn't be sent back to the operation, only the send was cancelled.
                    // The caller who originally queued the send will handle the operation itself.
                }
                catch (Exception ex)
                {
                    // Catch serialization and other similar errors and forward them to the operation
                    request.Operation.TrySetException(ex);
                }
            };
        }

        /// <summary>
        /// Locks the collection, removes any dead connections, and replaces them.
        /// </summary>
        /// <returns></returns>
        private async Task CleanupDeadConnectionsAsync()
        {
            await _lock.WaitAsync(_cts.Token).ConfigureAwait(false);
            try
            {
                var deadCount = 0;

                for (var i = 0; i < _connections.Count; i++)
                {
                    if (_connections[i].Connection.IsDead)
                    {
                        _connections[i].Block.Complete();
                        _connections[i].Connection.Dispose();
                        _connections.RemoveAt(i);

                        deadCount++;
                        i--;
                    }
                }

                if (deadCount > 0)
                {
                    _logger.LogInformation("Connection pool for {endpoint} has {size} dead connections, removing.",
                        _redactor.SystemData(EndPoint), deadCount);
                }

                // Ensure that we still meet the minimum size
                var needToRestart = MinimumSize - _connections.Count;
                if (needToRestart > 0)
                {
                    try
                    {
                        await AddConnectionsAsync(needToRestart, _cts.Token).ConfigureAwait(false);

                        _logger.LogInformation("Restarted {size} connections for {endpoint}.",
                            needToRestart, _redactor.SystemData(EndPoint));
                    }
                    catch (Exception ex)
                    {
                        // Eat the error if we were unable to restart one of the dead connections, but log
                        _logger.LogError(ex, "Error replacing dead connections for {endpoint}.", _redactor.SystemData(EndPoint));
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        #endregion

        private class FreezeDisposable : IAsyncDisposable
        {
            private readonly DataFlowConnectionPool _connectionPool;

            public FreezeDisposable(DataFlowConnectionPool connectionPool)
            {
                _connectionPool = connectionPool;
            }

            public ValueTask DisposeAsync()
            {
                _connectionPool._lock.Release();

                return default;
            }
        }

        [StructLayout(LayoutKind.Auto)]
        private struct QueueItem
        {
            public IOperation Operation { get; set; }
            public CancellationToken CancellationToken { get; set; }
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
