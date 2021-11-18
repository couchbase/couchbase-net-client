using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Diagnostics;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using IRequestSpan = Couchbase.Core.Diagnostics.Tracing.IRequestSpan;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    internal class MultiplexingConnection : IConnection
    {
        private static readonly ConcurrentBag<WeakReference<MultiplexingConnection>> _connections = new();

        public static int GetConnectionCount() => _connections.Count(p => p.TryGetTarget(out var connection) && !connection.IsDead);

        private const uint MaxDocSize = 20971520;
        private readonly Stream _stream;
        private readonly ILogger<MultiplexingConnection> _logger;
        private readonly InFlightOperationSet _statesInFlight = new(TimeSpan.FromSeconds(75));
        private LightweightStopwatch _stopwatch;
        private int _disposed;

        private readonly string _remoteHostString;
        private readonly string _localHostString;
        private readonly string _remotePortString;
        private readonly string _localPortString;

        // Connection pooling normally prevents simultaneous writes, but there are cases where they may occur,
        // such as when running Diagnostics pings. We therefore need to prevent them ourselves, as the internal
        // implementation of socket writes may interleave large buffers written from different threads.
        private readonly SemaphoreSlim _writeMutex = new(1);

        public MultiplexingConnection(Socket socket, ILogger<MultiplexingConnection> logger)
            : this(new NetworkStream(socket, true), socket.LocalEndPoint!, socket.RemoteEndPoint!, logger)
        {
        }

        public MultiplexingConnection(Stream stream, EndPoint localEndPoint, EndPoint remoteEndPoint,
            ILogger<MultiplexingConnection> logger)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            LocalEndPoint = localEndPoint ?? throw new ArgumentNullException(nameof(localEndPoint));
            EndPoint = remoteEndPoint ?? throw new ArgumentNullException(nameof(remoteEndPoint));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            ConnectionId = ConnectionIdProvider.GetRandomLong();
            ContextId = ClientIdentifier.FormatConnectionString(ConnectionId);

            _remoteHostString = ((IPEndPoint) EndPoint).Address.ToString() ?? DiagnosticsReportProvider.UnknownEndpointValue;
            _localHostString = ((IPEndPoint) LocalEndPoint).Address.ToString() ?? DiagnosticsReportProvider.UnknownEndpointValue;
            _remotePortString = ((IPEndPoint) EndPoint).Port.ToString();
            _localPortString = ((IPEndPoint) LocalEndPoint).Port.ToString();

            _stopwatch = LightweightStopwatch.StartNew();

            // We don't need the execution context to flow to the receive loop
            bool restoreFlow = false;
            try
            {
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                Task.Run(ReceiveResponsesAsync);
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }

            _connections.Add(new WeakReference<MultiplexingConnection>(this, false));
        }

        public string ContextId { get; }

        /// <inheritdoc />
        public ulong ConnectionId { get; }

        /// <inheritdoc />
        public bool IsConnected => !IsDead;

        /// <inheritdoc />
        public EndPoint EndPoint { get; }

        /// <inheritdoc />
        public EndPoint LocalEndPoint { get; }

        /// <inheritdoc />
        public bool IsAuthenticated { get; set; }

        /// <inheritdoc />
        public bool IsSecure => false;

        /// <inheritdoc />
        public bool IsDead => Volatile.Read(ref _disposed) > 0;

        /// <inheritdoc />
        public ServerFeatureSet ServerFeatures { get; set; } = ServerFeatureSet.Empty;

        /// <inheritdoc />
        public async ValueTask SendAsync(ReadOnlyMemory<byte> request, IOperation operation, CancellationToken cancellationToken = default)
        {
            if (request.Length >= MaxDocSize)
            {
                throw new ValueToolargeException("Encoded document exceeds the 20MB document size limit.");
            }
            if (Volatile.Read(ref _disposed) > 0)
            {
                ThrowHelper.ThrowObjectDisposedException(nameof(MultiplexingConnection));
            }

            var state = new AsyncState(operation)
            {
                EndPoint = EndPoint,
                ConnectionId = ConnectionId,
                LocalEndpoint = _localHostString
            };

            _statesInFlight.Add(state);

            await _writeMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
#if SPAN_SUPPORT
                await _stream.WriteAsync(request, cancellationToken).ConfigureAwait(false);
#else
                if (!MemoryMarshal.TryGetArray<byte>(request, out var arraySegment))
                {
                    // Fallback in case we can't use the more efficient TryGetArray method
                    arraySegment = new ArraySegment<byte>(request.ToArray());
                }

                await _stream.WriteAsync(arraySegment.Array, arraySegment.Offset, arraySegment.Count, cancellationToken)
                    .ConfigureAwait(false);
#endif
            }
            catch (OperationCanceledException)
            {
                // Don't let cancellations kill the connection, just ignore.
                // It's also unnecessary to forward this to the operation, as it is monitoring for cancellation
                // and throws the correct exception type based on internal vs. external cancellation.
            }
            catch (Exception e)
            {
                HandleDisconnect(e);
            }
            finally
            {
                _writeMutex.Release();
            }
        }

        /// <summary>
        /// Continuously running task which constantly listens for responses
        /// coming back from the server and processes the responses once complete.
        /// </summary>
        internal async Task ReceiveResponsesAsync()
        {
            // bufferSize is the minimum buffer size to rent from ArrayPool<byte>.Shared. We can actually
            // buffer much more than this value, it is merely the size of the buffer segments which will be used.
            // When operations larger than this size are encountered, additional segments will be requested from
            // the pool and retained only until that operation is completed.
            //
            // minimumReadSize is the minimum block of data to read into a buffer segment from the stream. If there is
            // not enough space left in a segment, a new segment will be requested from the pool. This is set to 1500
            // to match the default IP MTU on most systems.
            var reader = PipeReader.Create(_stream, new StreamPipeReaderOptions(
                bufferSize: 65536,
                minimumReadSize: 1500));

            try
            {
                while (true)
                {
                    ReadResult result = await reader.ReadAsync().ConfigureAwait(false);
                    ReadOnlySequence<byte> buffer = result.Buffer;

                    // Process as many complete operation as we have in the buffer
                    while (TryReadOperation(ref buffer, out SlicedMemoryOwner<byte> operationResponse))
                    {
                        try
                        {
                            var opaque = ByteConverter.ToUInt32(operationResponse.Memory.Span.Slice(HeaderOffsets.Opaque));

                            if (_statesInFlight.TryRemove(opaque, out var state))
                            {
                                state.Complete(in operationResponse);
                            }
                            else
                            {
                                operationResponse.Dispose();
                            }
                        }
                        catch
                        {
                            // Ownership of the buffer was not accepted by state.Complete due to an exception
                            // Make sure we release the buffer
                            operationResponse.Dispose();
                            throw;
                        }

                        UpdateLastActivity();
                    }

                    // Tell the reader how much data we've actually consumed, the rest will remain for the next read
                    reader.AdvanceTo(buffer.Start, buffer.End);

                    // Stop reading if there's no more data
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }

                HandleDisconnect(null);
            }
#if NET452
            catch (ThreadAbortException) {}
#endif
            catch (ObjectDisposedException) {}
            catch (Exception e)
            {
                HandleDisconnect(e);
            }
            finally
            {
                await reader.CompleteAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Parses the received data checking the buffer to see if a completed response has arrived.
        /// If it has, the operation is copied to a new, complete buffer and true is returned.
        /// </summary>
        internal bool TryReadOperation(ref ReadOnlySequence<byte> buffer, out SlicedMemoryOwner<byte> operationResponse)
        {
            if (buffer.Length < HeaderOffsets.HeaderLength)
            {
                // Not enough data to read the body length from the header
                operationResponse = default;
                return false;
            }

            int responseSize = HeaderOffsets.HeaderLength;
            var sizeSegment = buffer.Slice(HeaderOffsets.BodyLength, sizeof(int));
            if (sizeSegment.IsSingleSegment)
            {
                responseSize += ByteConverter.ToInt32(sizeSegment.First.Span);
            }
            else
            {
                // Edge case, we're split across segments in the buffer
                Span<byte> tempSpan = stackalloc byte[sizeof(int)];

                sizeSegment.CopyTo(tempSpan);

                responseSize += ByteConverter.ToInt32(tempSpan);
            }

            if (buffer.Length < responseSize)
            {
                // Insufficient data, keep filling the buffer
                operationResponse = default;
                return false;
            }

            // Slice to get operationBuffer, which is just the operation
            // And slice the original buffer to start after this operation,
            // we pass this back by ref so it's ready for the next operation

            var position = buffer.GetPosition(responseSize);
            var operationBuffer = buffer.Slice(0, position);
            buffer = buffer.Slice(position);

            // Copy the response to a separate, contiguous memory buffer

            operationResponse = MemoryPool<byte>.Shared.RentAndSlice(responseSize);
            try
            {
                operationBuffer.CopyTo(operationResponse.Memory.Span);
                return true;
            }
            catch
            {
                // Cleanup the memory in case of exception
                operationResponse.Dispose();
                throw;
            }
        }

        //our receive thread will always find out immediately when connection closes
        //we can easily catch the spots that mean connection was closed from the other
        //side and attach handling here
        private void HandleDisconnect(Exception? exception)
        {
            if (exception != null && Volatile.Read(ref _disposed) == 0)
            {
                //you might throw an event here to be handled by owner of this class, or can implement reconnect directly here etc...
                _logger.LogDebug(exception, "Handling disconnect for connection {cid}", ConnectionId);
            }

            //in any case the current socket object should be closed, all states in flight released etc.
            Close();
        }

        /// <inheritdoc />
        public TimeSpan IdleTime => _stopwatch.Elapsed;

        public string RemoteHost => _remoteHostString;

        public string LocalHost => _localHostString;

        private void UpdateLastActivity()
        {
            _stopwatch.Restart();
        }

        public void Close()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _logger.LogInformation("Closing connection {cid}", ConnectionId);

                try
                {
                    _stream?.Close();
                }
                catch (Exception e)
                {
                    _logger.LogInformation(e, string.Empty);
                }
                finally
                {
                    _stream?.Dispose();
                }

                _statesInFlight.Dispose();
            }
        }

        /// <inheritdoc />
        public async ValueTask CloseAsync(TimeSpan timeout)
        {
            if (Volatile.Read(ref _disposed) > 0)
            {
                return;
            }

            _logger.LogInformation("Closing connection {cid}, waiting {timeout} for {count} in-flight operations to complete.", ConnectionId, timeout, _statesInFlight.Count);

            try
            {
                await _statesInFlight.WaitForAllOperationsAsync(timeout).ConfigureAwait(false);

                Debug.Assert(_statesInFlight.Count == 0, "Expect no in-flight operations");
                _logger.LogInformation("In-flight operations are complete on connection {cid}, proceeding to close.", ConnectionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for all operations to gracefully complete before connection close.");
            }

            Close();
        }

        public void Dispose() => Close();

        /// <inheritdoc />
        public void AddTags(IRequestSpan span)
        {
            if (span.CanWrite)
            {
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalHostname, _localHostString);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalPort, _localPortString);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemoteHostname, _remoteHostString);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.RemotePort, _remotePortString);
                span.SetAttribute(InnerRequestSpans.DispatchSpan.Attributes.LocalId, ContextId);
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
