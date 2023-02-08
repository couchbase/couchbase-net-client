using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

#nullable enable

namespace Couchbase.Core.IO.Connections.Channels
{
    /// <summary>
    /// Reads the queue from a <see cref="ChannelConnectionPool" /> for a specific connection,
    /// greedily processing any operations queued.
    /// </summary>
    internal class ChannelConnectionProcessor
    {
        /// <summary>
        /// Time to wait for a graceful shutdown of a connection.
        /// </summary>
        private static readonly TimeSpan CloseTimeout = TimeSpan.FromMinutes(1);

        private readonly ChannelConnectionPool _connectionPool;
        private readonly ChannelReader<ChannelQueueItem> _channelReader;
        private readonly ILogger<ChannelConnectionPool> _logger;
        private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private CancellationTokenSource? _cts = new();

        /// <summary>
        /// Connection managed by this class.
        /// </summary>
        public IConnection Connection { get; }

        /// <summary>
        /// Task which is completed when processing items for this connection is completed.
        /// </summary>
        /// <remarks>
        /// This indicates that the queue is no longer being read and new operations are no
        /// longer being sent to the connection. It does not necessarily indicate that al
        /// in-flight operations on the connection are complete.
        /// </remarks>
        public Task Completion => _completion.Task;

        /// <summary>
        /// True when processing items for this connection is completed.
        /// </summary>
        /// <remarks>
        /// This indicates that the queue is no longer being read and new operations are no
        /// longer being sent to the connection. It does not necessarily indicate that al
        /// in-flight operations on the connection are complete.
        /// </remarks>
        public bool IsComplete => Completion.IsCompleted;

        /// <summary>
        /// Creates a new ChannelConnectionProcessor.
        /// </summary>
        /// <param name="connection">Connection to be managed.</param>
        /// <param name="connectionPool">Connection pool this connection belongs to.</param>
        /// <param name="channelReader">Reader from which to dequeue operations to be sent.</param>
        /// <param name="logger">Logger.</param>
        public ChannelConnectionProcessor(IConnection connection, ChannelConnectionPool connectionPool,
            ChannelReader<ChannelQueueItem> channelReader, ILogger<ChannelConnectionPool> logger)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (connection == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(connection));
            }

            if (connectionPool == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(connectionPool));
            }

            if (channelReader == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(channelReader));
            }

            if (logger == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(logger));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            Connection = connection;
            _connectionPool = connectionPool;
            _channelReader = channelReader;
            _logger = logger;
        }

        public ChannelConnectionProcessor Start()
        {
            using (ExecutionContext.SuppressFlow())
            {
                _ = Process();
            }

            return this;
        }

        /// <summary>
        /// Long running task to process items from the queue.
        /// </summary>
        internal async Task Process()
        {
            var token = _cts?.Token ?? default(CancellationToken);

            try
            {
                // Avoid object[] heap allocations if trace logging is disabled
                var traceLogging = _logger.IsEnabled(LogLevel.Trace);

                if (token == CancellationToken.None)
                {
                    // We're already disposed before this method was executed
                    return;
                }

                while (await _channelReader.WaitToReadAsync(token).ConfigureAwait(false))
                {
                    if (traceLogging)
                    {
                        _logger.LogTrace("Operations available for {cid}", Connection.ConnectionId);
                    }

                    // Check the connection to make sure the connection's alive before we take an item from the queue
                    if (Connection.IsDead)
                    {
                        break;
                    }

                    // Keep processing messages until the queue is empty, avoiding extraneous WaitToReadAsync calls
                    while (_channelReader.TryRead(out var queueItem))
                    {
                        try
                        {
                            // ignore request that timed out or was cancelled while in queue
                            if (queueItem.CancellationToken.IsCancellationRequested)
                            {
                                continue; //avoid closing the Connection because of an item that has timeout while in queue
                            }

                            if (traceLogging)
                            {
                                _logger.LogTrace("Sending operation {opaque} on {cid}", queueItem.Operation.Opaque,
                                    Connection.ConnectionId);
                            }

                            // ReSharper disable once MethodSupportsCancellation
                            // We don't want to forward the cancellation token for this connection, as that isn't the
                            // same as the cancellation token for the operation. If this connection is being shutdown
                            // while the operation is being sent, we'll let it finish sending.
                            await queueItem.Operation.SendAsync(Connection).ConfigureAwait(false);

                            if (traceLogging)
                            {
                                _logger.LogTrace("Sent operation {opaque} on {cid}", queueItem.Operation.Opaque,
                                    Connection.ConnectionId);
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
                            queueItem.Operation.TrySetException(ex);
                        }

                        // Stop processing if we're completed, and recheck the connection before we take another item from the queue
                        if (token.IsCancellationRequested || Connection.IsDead)
                        {
                            break;
                        }
                    }
                }
            }
            finally
            {
                _logger.LogInformation("Done processing operations on {cid}, IsDead: {isDead}", Connection.ConnectionId, Connection.IsDead);

                // Mark the connection processor complete
                _completion.SetResult(true);

                // Remove the connection, unless it we're completed (due to the pool closing or shrinking).
                // This will cleanup references, and replace the connection if necessary.
                if (token != CancellationToken.None && !token.IsCancellationRequested)
                {
                    try
                    {
                        await _connectionPool.RemoveConnectionAsync(this).ConfigureAwait(false);
                    }
                    catch
                    {
                        //Ensure that any error thrown by RemoveConnectionAsync will not prevent Connection.CloseAsync
                        _logger.LogInformation("Connection {cid} was not removed.", Connection.ConnectionId);
                    }
                }
                // Let in-flight operations finish, waiting up to one minute
                await Connection.CloseAsync(CloseTimeout).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Stop processing new operations from the queue.
        /// </summary>
        public Task CompleteAsync()
        {
            var cts = Interlocked.Exchange(ref _cts, null);

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }

            return Completion;
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
