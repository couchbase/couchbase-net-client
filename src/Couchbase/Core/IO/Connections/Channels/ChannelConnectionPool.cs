using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections.Channels
{
    /// <summary>
    /// Connection pool based on queuing operations via the TPL data flows library.
    /// </summary>
    internal sealed partial class ChannelConnectionPool : ConnectionPoolBase
    {
        internal static readonly TimeSpan MinRefillDelay = TimeSpan.FromMilliseconds(32);
        internal static readonly TimeSpan MaxRefillDelay = TimeSpan.FromMilliseconds(4096);

        private readonly IConnectionPoolScaleController _scaleController;
        private readonly Redactor _redactor;
        private readonly ILogger<ChannelConnectionPool> _logger;
        private readonly TimeProvider _timeProvider;
        private readonly CancellationTokenSource _cts = new();

        // Read once, because the Token getter throws after _cts is disposed.
        private readonly CancellationToken _poolToken;

        // Serializes connects, scaling and bucket selection. It is never disposed, because an attempt can still hold it after Dispose.
        private readonly SemaphoreSlim _lock = new(1);

        // Guards _connections, _initialized, _disposed and _refillRunning. It is never held across an await.
        private readonly object _stateLock = new();

        // Replaced as a whole, so readers take no lock.
        private ChannelConnectionProcessor[] _connections = [];

        private readonly Channel<ChannelQueueItem> _sendQueue;

        private bool _initialized;
        private bool _disposed;
        private bool _refillRunning;
        private int _refillStarts;

        /// <summary>
        /// For UNIT TESTING ONLY. How many refill loops this pool has started.
        /// </summary>
        internal int RefillStarts => Volatile.Read(ref _refillStarts);

        /// <summary>
        /// For UNIT TESTING ONLY. Whether a refill loop runs. A loop decides to stop before it releases the pool lock.
        /// </summary>
        internal bool IsRefilling
        {
            get
            {
                lock (_stateLock)
                {
                    return _refillRunning;
                }
            }
        }

        /// <inheritdoc />
        public override int Size => Volatile.Read(ref _connections).Length;

        /// <inheritdoc />
        public override int MinimumSize { get; set; }

        /// <inheritdoc />
        public override int MaximumSize { get; set; }

        /// <inheritdoc />
        public override int PendingSends => _sendQueue.Reader.Count;

        /// <summary>
        /// Creates a new ChannelConnectionPool.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="scaleController">Scale controller.</param>
        /// <param name="redactor">Log redactor.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="timeProvider">Clock for the wait between refill attempts.</param>
        /// <param name="sendQueueCapacity">The maximum number of items the channel will store.
        /// Defaults to 1024 and is configurable via <see cref="Couchbase.ClusterOptions.KvSendQueueCapacity"/></param>
        /// <param name="bucketName">The bucket that each new connection selects, or null for none.</param>
        public ChannelConnectionPool(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory,
            IConnectionPoolScaleController scaleController, Redactor redactor, ILogger<ChannelConnectionPool> logger,
            TimeProvider timeProvider, int sendQueueCapacity, string? bucketName = null) :
            this(connectionInitializer, connectionFactory, scaleController, redactor, logger, timeProvider,
                CreateDefaultChannel(sendQueueCapacity), bucketName)
        {
        }

        /// <summary>
        /// Creates a new ChannelConnectionPool.
        /// </summary>
        /// <param name="connectionInitializer">Handler for initializing new connections.</param>
        /// <param name="connectionFactory">Factory for creating new connections.</param>
        /// <param name="scaleController">Scale controller.</param>
        /// <param name="redactor">Log redactor.</param>
        /// <param name="logger">Logger.</param>
        /// <param name="timeProvider">Clock for the wait between refill attempts.</param>
        /// <param name="channel">Channel queue.</param>
        /// <param name="bucketName">The bucket that each new connection selects, or null for none.</param>
        internal ChannelConnectionPool(IConnectionInitializer connectionInitializer, IConnectionFactory connectionFactory,
            IConnectionPoolScaleController scaleController, Redactor redactor, ILogger<ChannelConnectionPool> logger,
            TimeProvider timeProvider, Channel<ChannelQueueItem> channel, string? bucketName = null)
            : base(connectionInitializer, connectionFactory, logger, bucketName)
        {
            _scaleController = scaleController ?? throw new ArgumentNullException(nameof(scaleController));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            _poolToken = _cts.Token;

            MinimumSize = 2;
            MaximumSize = 5;

            _sendQueue = channel;

            TrackConnectionPool(this);
        }

        private static Channel<ChannelQueueItem> CreateDefaultChannel(int sendQueueCapacity)
        {
            return Channel.CreateBounded<ChannelQueueItem>(new BoundedChannelOptions(sendQueueCapacity)
            {
                AllowSynchronousContinuations = true
            });
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns when the pool has one live connection. The refill loop opens the rest in the background.
        /// Throws the error of the first round when it ends with no live connection. The loop then keeps trying
        /// until the pool is at its minimum size, so a caller that does not keep the pool must dispose it.
        /// </remarks>
        public override async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _initialized))
            {
                return;
            }

            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var firstRound = new TaskCompletionSource<ExceptionDispatchInfo?>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_stateLock)
            {
                if (_refillRunning)
                {
                    throw new InvalidOperationException($"The connection pool for {EndPoint} is already connecting.");
                }

                _refillRunning = true;
            }

            StartRefill(firstRound);

            ExceptionDispatchInfo? error;
            try
            {
                error = await firstRound.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Tells the loop to stop after its first round.
                firstRound.TrySetCanceled(cancellationToken);
                throw;
            }

            error?.Throw();

            MarkInitialized();
        }

        /// <summary>
        /// Starts the scale controller once, at the first live connection of the pool.
        /// </summary>
        private void MarkInitialized()
        {
            lock (_stateLock)
            {
                if (_initialized || _disposed)
                {
                    return;
                }

                // Under the lock, so Dispose cannot stop the controller before it starts.
                _scaleController.Start(this);
                Volatile.Write(ref _initialized, true);
            }

            InitializedConnectionPool(_redactor.SystemData(EndPoint), Size);
        }

        /// <inheritdoc />
        public override Task SendAsync(IOperation operation, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            // Note: Because synchronous continuations are enabled, The ChannelConnectionProcessor's work to process items
            // from the queue may take place synchronously on this thread and momentarily block the return of this method.
            // However, this gives us the performance benefit of not queuing the send work on the thread pool. The time
            // involved is the time to serialize an operation to a buffer and send it to the socket (but not the actual
            // send over the network). Note, however, that the operation serialized may not be the operation being sent here,
            // it will be the next operation in the queue. So a large operation may block very briefly, and later a small
            // operation may block longer.
            if (!_sendQueue.Writer.TryWrite(new ChannelQueueItem(operation, cancellationToken)))
            {
                MetricTracker.KeyValue.TrackSendQueueFull();
                ThrowHelper.ThrowSendQueueFullException();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public override async Task<bool> TrySendImmediatelyAsync(IOperation op, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            var connection = GetRandomLiveConnection();
            if (connection is null)
            {
                // A queued send can wait in a queue that no processor reads, so fail at once and let the caller retry.
                ThrowHelper.ThrowSocketNotAvailableException(nameof(ChannelConnectionPool));
            }

            // Unlike the queue, pass the token, so the send stops waiting for a busy connection when it fires.
            await op.SendAsync(connection, cancellationToken).ConfigureAwait(false);

            return true;
        }

        private IConnection? GetRandomLiveConnection()
        {
            var connections = Volatile.Read(ref _connections);
            if (connections.Length == 0)
            {
                return null;
            }

            var start = ArrayExtensions.GetRandomInt32(connections.Length);
            for (var i = 0; i < connections.Length; i++)
            {
                var processor = connections[(start + i) % connections.Length];
                if (!processor.IsComplete && !processor.Connection.IsDead)
                {
                    return processor.Connection;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public override IEnumerable<IConnection> GetConnections()
        {
            EnsureNotDisposed();

            var connections = Volatile.Read(ref _connections);
            var result = new List<IConnection>(connections.Length);
            foreach (var processor in connections)
            {
                if (!processor.IsComplete)
                {
                    result.Add(processor.Connection);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public override async Task ScaleAsync(int delta)
        {
            if (delta > 0)
            {
                var growBy = Math.Min(delta, MaximumSize - Size);
                if (growBy > 0)
                {
                    await AddConnectionsAsync(growBy, _poolToken).ConfigureAwait(false);
                }
            }
            else if (delta < 0)
            {
                var connections = Volatile.Read(ref _connections);
                var shrinkBy = Math.Min(-delta, connections.Length - MinimumSize);
                if (shrinkBy > 0)
                {
                    // Select connections to shrink, longest inactive first
                    var toShrink = connections
                        .OrderByDescending(static p => p.Connection.IdleTime)
                        .Take(shrinkBy)
                        .ToList();

                    // Stop all connections from receiving new sends, and wait for in flight sends
                    // to complete, in parallel
                    await Task.WhenAll(toShrink.Select(static p => p.CompleteAsync())).ConfigureAwait(false);

                    lock (_stateLock)
                    {
                        foreach (var p in toShrink)
                        {
                            _connections = Without(_connections, p);
                        }
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
                ThrowHelper.ThrowSocketNotAvailableException(nameof(ChannelConnectionPool));
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Does not wait for a connect in progress. A connection that opens after this is disposed and not added.
        /// </remarks>
        public override void Dispose()
        {
            UntrackConnectionPool();

            ChannelConnectionProcessor[] connections;
            bool alreadyDisposed;
            lock (_stateLock)
            {
                alreadyDisposed = _disposed;
                _disposed = true;
                connections = _connections;
                _connections = [];
            }

            if (alreadyDisposed)
            {
                LogAlreadyDisposedConnectionPool(EndPoint);
                return;
            }

            LogDisposeConnectionPool(EndPoint);
            _scaleController.Dispose();
            _cts.Cancel(false);

            // Complete any queued commands
            _sendQueue.Writer.TryComplete();

            // Dispose of the connections. The ChannelConnectionProcessor should do this,
            // but we'd like to be more proactive in this case.
            foreach (var processor in connections)
            {
                processor.Connection.Dispose();
            }

            _cts.Dispose();
        }

        #region Connection Management

        /// <summary>
        /// Opens connections in parallel and adds each one that is live. Assumes that the pool is already locked.
        /// </summary>
        /// <param name="count">Number of connections to add.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="firstConnection">Completed with no error when a connection is added.</param>
        /// <returns>The number of connections added.</returns>
        /// <remarks>
        /// Throws the first error when a connect fails, after every connect has ended. The other connections stay added.
        /// </remarks>
        private async Task<int> AddConnectionsAsync(int count, CancellationToken cancellationToken,
            TaskCompletionSource<ExceptionDispatchInfo?>? firstConnection = null)
        {
            if (count <= 0)
            {
                return 0;
            }

            var tasks = new Task<bool>[count];
            for (var i = 0; i < count; i++)
            {
                tasks[i] = AddConnectionAsync(cancellationToken, firstConnection);
            }

            var results = await Task.WhenAll(tasks).ConfigureAwait(false);

            var added = 0;
            foreach (var result in results)
            {
                if (result)
                {
                    added++;
                }
            }

            return added;
        }

        private async Task<bool> AddConnectionAsync(CancellationToken cancellationToken,
            TaskCompletionSource<ExceptionDispatchInfo?>? firstConnection)
        {
            var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

            if (connection.IsDead)
            {
                // Already connected, so it owns a socket even though it is unusable.
                LogConnectionToEndpointError(EndPoint);
                connection.Dispose();
                return false;
            }

            var processor = new ChannelConnectionProcessor(connection, this, _sendQueue.Reader, _logger);
            bool added;
            lock (_stateLock)
            {
                added = !_disposed;
                if (added)
                {
                    _connections = With(_connections, processor);
                }
            }

            if (!added)
            {
                connection.Dispose();
                return false;
            }

            LogConnectionToEndpointSuccess(EndPoint);

            // Started after it is added, so a removal on a fast close always finds it.
            processor.Start();

            firstConnection?.TrySetResult(null);
            return true;
        }

        /// <summary>
        /// Removes a processor whose connection closed, and starts the refill loop when none runs.
        /// </summary>
        public void RemoveConnection(ChannelConnectionProcessor connection)
        {
            bool start;
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                _connections = Without(_connections, connection);

                start = !_refillRunning && _connections.Length < MinimumSize;
                if (start)
                {
                    _refillRunning = true;
                }
            }

            if (start)
            {
                StartRefill(firstRound: null);
            }
        }

        private void StartRefill(TaskCompletionSource<ExceptionDispatchInfo?>? firstRound)
        {
            Interlocked.Increment(ref _refillStarts);

            // A connect that completes at once starts processors inside this call, which suppress the flow too.
            // Suppressing twice on one thread throws.
            if (ExecutionContext.IsFlowSuppressed())
            {
                _ = RefillAsync(firstRound);
                return;
            }

            using (ExecutionContext.SuppressFlow())
            {
                _ = RefillAsync(firstRound);
            }
        }

        /// <summary>
        /// Opens connections until the pool is at its minimum size or disposed. It waits between attempts with a
        /// backoff that grows until the loop ends.
        /// The caller sets <see cref="_refillRunning"/> before it starts the loop.
        /// </summary>
        /// <param name="firstRound">Completed at the first live connection, or with the error when the first round
        /// ends with none. The loop stops after its first round when this is cancelled.</param>
        private async Task RefillAsync(TaskCompletionSource<ExceptionDispatchInfo?>? firstRound)
        {
            var delay = MinRefillDelay;
            var failedAttempts = 0;
            var quickRetried = false;
            var running = true;
            var firstRoundFailed = false;
            var firstRoundThrown = false;
            ExceptionDispatchInfo? fatal = null;
            try
            {
                while (true)
                {
                    ExceptionDispatchInfo? error = null;
                    var failed = false;
                    var errorWentToCaller = false;
                    int size;

                    await _lock.WaitAsync(_poolToken).ConfigureAwait(false);
                    try
                    {
                        var missing = MinimumSize - Size;
                        if (missing > 0)
                        {
                            try
                            {
                                // A connection that comes back dead is not added, so it counts as a failure.
                                failed = await AddConnectionsAsync(missing, _poolToken, firstRound).ConfigureAwait(false) < missing;
                            }
                            catch (Exception ex)
                            {
                                error = ExceptionDispatchInfo.Capture(ex);
                                failed = true;
                            }
                        }

                        // The exit decision and the flag change share the lock with RemoveConnection, so no close is lost.
                        lock (_stateLock)
                        {
                            size = _connections.Length;
                            var initCancelled = firstRound is not null && firstRound.Task.IsCanceled;

                            if (_disposed || size >= MinimumSize || initCancelled)
                            {
                                _refillRunning = false;
                                running = false;
                            }
                        }
                    }
                    finally
                    {
                        _lock.Release();
                    }

                    if (firstRound is not null)
                    {
                        firstRoundFailed = size == 0 && MinimumSize > 0;
                        errorWentToCaller = firstRoundFailed;
                        firstRoundThrown = firstRoundFailed;
                        firstRound.TrySetResult(firstRoundFailed
                            ? error ?? ExceptionDispatchInfo.Capture(new ConnectException($"Unable to open any connections to {EndPoint}."))
                            : null);
                        firstRound = null;
                    }
                    else if (firstRoundFailed && size > 0)
                    {
                        // InitializeAsync threw, so the pool starts its scale controller at its first live connection.
                        firstRoundFailed = false;
                        MarkInitialized();
                    }

                    if (!running)
                    {
                        // The count includes a first round whose error InitializeAsync threw.
                        var failedTotal = failedAttempts + (firstRoundThrown ? 1 : 0);
                        if (failedTotal > 0 && size >= MinimumSize)
                        {
                            LogRefillRecovered(_redactor.SystemData(EndPoint), size, failedTotal);
                        }

                        return;
                    }

                    // Every connect worked, but a connection closed meanwhile. Retry at once only one time,
                    // so a node that closes each connection right after the handshake still gets the backoff.
                    if (!failed && !quickRetried)
                    {
                        quickRetried = true;
                        continue;
                    }

                    quickRetried = false;

                    // InitializeAsync throws this error, and a caller that drops the pool stops the retry it would announce.
                    if (!errorWentToCaller)
                    {
                        failedAttempts++;
                        LogRefillFailed(failedAttempts == 1 ? LogLevel.Warning : LogLevel.Debug, error?.SourceException,
                            _redactor.SystemData(EndPoint), size, MinimumSize, delay);
                    }

                    await _timeProvider.Delay(delay, _poolToken).ConfigureAwait(false);

                    delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, MaxRefillDelay.Ticks));
                }
            }
            catch (OperationCanceledException ex) when (_poolToken.IsCancellationRequested)
            {
                fatal = ExceptionDispatchInfo.Capture(ex);
            }
            catch (Exception ex)
            {
                fatal = ExceptionDispatchInfo.Capture(ex);
                LogRefillStopped(ex, _redactor.SystemData(EndPoint));
            }
            finally
            {
                firstRound?.TrySetResult(fatal);

                if (running)
                {
                    lock (_stateLock)
                    {
                        _refillRunning = false;
                    }
                }
            }
        }

        private static ChannelConnectionProcessor[] With(ChannelConnectionProcessor[] connections,
            ChannelConnectionProcessor processor)
        {
            var result = new ChannelConnectionProcessor[connections.Length + 1];
            Array.Copy(connections, result, connections.Length);
            result[connections.Length] = processor;
            return result;
        }

        private static ChannelConnectionProcessor[] Without(ChannelConnectionProcessor[] connections,
            ChannelConnectionProcessor processor)
        {
            var index = Array.IndexOf(connections, processor);
            if (index < 0)
            {
                return connections;
            }

            var result = new ChannelConnectionProcessor[connections.Length - 1];
            Array.Copy(connections, 0, result, 0, index);
            Array.Copy(connections, index + 1, result, index, result.Length - index);
            return result;
        }

        #endregion

        private class FreezeDisposable : IAsyncDisposable
        {
            private readonly ChannelConnectionPool _connectionPool;

            public FreezeDisposable(ChannelConnectionPool connectionPool)
            {
                _connectionPool = connectionPool;
            }

            public ValueTask DisposeAsync()
            {
                _connectionPool._lock.Release();

                return default;
            }
        }

        #region Logging

        [LoggerMessage(1, LogLevel.Debug, "Connection pool for {endpoint} initialized with {size} connections.")]
        private partial void InitializedConnectionPool(object endpoint, int size);

        [LoggerMessage(LoggingEvents.ChannelConnectionEvent, LogLevel.Debug, "Connection for {endpoint} has been started.")]
        private partial void LogConnectionToEndpointSuccess(HostEndpointWithPort endpoint);

        [LoggerMessage(100, LogLevel.Debug, "Connection for {endpoint} could not be started.")]
        private partial void LogConnectionToEndpointError(HostEndpointWithPort endpoint);

        [LoggerMessage(101, LogLevel.Debug, "Disposing pool for {endpoint}.")]
        private partial void LogDisposeConnectionPool(HostEndpointWithPort endpoint);

        [LoggerMessage(102, LogLevel.Debug, "Disposing pool for {endpoint} that was already disposed")]
        private partial void LogAlreadyDisposedConnectionPool(HostEndpointWithPort endpoint);

        [LoggerMessage(EventId = 104, Message = "Connection pool for {endpoint} has {size} of {minimumSize} connections. The next attempt is in {delay}.")]
        private partial void LogRefillFailed(LogLevel level, Exception? exception, object endpoint, int size, int minimumSize, TimeSpan delay);

        [LoggerMessage(105, LogLevel.Information, "Connection pool for {endpoint} opened its connections and has {size} after {failedAttempts} failed attempts.")]
        private partial void LogRefillRecovered(object endpoint, int size, int failedAttempts);

        [LoggerMessage(106, LogLevel.Error, "Connection pool for {endpoint} stopped opening connections after an unexpected error.")]
        private partial void LogRefillStopped(Exception exception, object endpoint);

        #endregion
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
