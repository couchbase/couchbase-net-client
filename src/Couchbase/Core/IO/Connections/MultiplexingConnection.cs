using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Converters;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    internal class MultiplexingConnection : IConnection
    {
        private readonly ILogger<MultiplexingConnection> _logger;
        private readonly ConcurrentDictionary<uint, IState> _statesInFlight;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly Thread _receiveThread;
        private byte[] _receiveBuffer;
        private int _receiveBufferLength;
        private readonly object _syncObj = new object();
        private volatile bool _disposed;

        public MultiplexingConnection(Socket socket, ILogger<MultiplexingConnection> logger)
        {
            Socket = socket ?? throw new ArgumentNullException(nameof(socket));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            LocalEndPoint = socket.LocalEndPoint;
            EndPoint = socket.RemoteEndPoint;

            _statesInFlight = new ConcurrentDictionary<uint, IState>();

            //allocate a buffer
            _receiveBuffer = new byte[1024 * 16];
            _receiveBufferLength = 0;

            ConnectionId = ConnectionIdProvider.GetNextId();

            //Start a dedicated background thread for receiving server responses.
            _receiveThread = new Thread(ReceiveThreadBody)
            {
                IsBackground = true
            };
            _receiveThread.Start();
        }

        /// <inheritdoc />
        public ulong ConnectionId { get; }

        private Socket Socket { get; }

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
        public bool IsDead { get; set; }

        /// <inheritdoc />
        public Task SendAsync(ReadOnlyMemory<byte> request, Func<SocketAsyncState, Task> callback, ErrorMap? errorMap = null)
        {
            var opaque = ByteConverter.ToUInt32(request.Span.Slice(HeaderOffsets.Opaque));
            var state = new AsyncState
            {
                Opaque = opaque,
                Callback = callback,
                EndPoint = (IPEndPoint)EndPoint,
                ConnectionId = ConnectionId,
                ErrorMap = errorMap,
                LocalEndpoint = LocalEndPoint.ToString()
            };

            _statesInFlight.TryAdd(state.Opaque, state);

            state.Timer = new Timer(o =>
            {
                AsyncState a = (AsyncState)o;
                _statesInFlight.TryRemove(a.Opaque, out _);
                a.Cancel(ResponseStatus.OperationTimeout, new TimeoutException());
            }, state, 75000, Timeout.Infinite);


            lock (Socket)
            {
                try
                {
                    #if NETCOREAPP2_1 || NETSTANDARD2_1

                    var requestSpan = request.Span;
                    while (requestSpan.Length > 0) {
                        var sentBytesCount = Socket.Send(requestSpan, SocketFlags.None);

                        requestSpan = requestSpan.Slice(sentBytesCount);
                    }

                    #else

                    if (!MemoryMarshal.TryGetArray<byte>(request, out var arraySegment))
                    {
                        // Fallback in case we can't use the more efficient TryGetArray method
                        arraySegment = new ArraySegment<byte>(request.ToArray());
                    }

                    var sentBytesCount = 0;
                    do
                    {
                        // ReSharper disable once AssignNullToNotNullAttribute
                        sentBytesCount += Socket.Send(arraySegment.Array,
                            arraySegment.Offset + sentBytesCount,
                            arraySegment.Count - sentBytesCount,
                            SocketFlags.None);
                    } while (sentBytesCount < arraySegment.Count);

                    #endif
                }
                catch (Exception e)
                {
                    HandleDisconnect(e);
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Executed by a dedicated background thread to constantly listen for responses
        /// coming back from the server and writes them to the <see cref="_receiveBuffer"/>.
        /// </summary>
        internal void ReceiveThreadBody()
        {
            try
            {
                while (Socket.Connected)
                {
                    if (_receiveBuffer.Length < _receiveBufferLength*2)
                    {
                        var buffer = new byte[_receiveBuffer.Length*2];
                        Buffer.BlockCopy(_receiveBuffer, 0, buffer, 0, _receiveBufferLength);
                        _receiveBuffer = buffer;
                    }

                    var receivedByteCount = Socket.Receive(_receiveBuffer, _receiveBufferLength,
                        _receiveBuffer.Length - _receiveBufferLength, SocketFlags.None);

                    if (receivedByteCount == 0) break;

                    _receiveBufferLength += receivedByteCount;

                    ParseReceivedData();
                }
                HandleDisconnect(new Exception("socket closed."));
            }
#if NET452
            catch (ThreadAbortException) {}
#endif
            catch (ObjectDisposedException) {}
            catch (SocketException e)
            {
                //Dispose has already been thrown by another thread
                if ((int) e.SocketErrorCode != 10004)
                {
                    HandleDisconnect(e);
                }
            }
            catch (Exception e)
            {
                HandleDisconnect(e);
            }
        }

        /// <summary>
        /// Parses the received data checking the buffer to see if a completed response has arrived.
        ///  If it has, the request is completed and the <see cref="IState"/> is removed from the pending queue.
        /// </summary>
        internal void ParseReceivedData()
        {
            var parsedOffset = 0;
            while (parsedOffset + HeaderOffsets.BodyLength < _receiveBufferLength)
            {
                var responseSize = ByteConverter.ToInt32(_receiveBuffer.AsSpan(parsedOffset + HeaderOffsets.BodyLength)) + 24;
                if (parsedOffset + responseSize > _receiveBufferLength) break;

                var opaque = ByteConverter.ToUInt32(_receiveBuffer.AsSpan(parsedOffset + HeaderOffsets.Opaque));
                var response = MemoryPool<byte>.Shared.RentAndSlice(responseSize);
                try
                {
                    _receiveBuffer.AsMemory(parsedOffset, responseSize).CopyTo(response.Memory);

                    parsedOffset += responseSize;

                    if (_statesInFlight.TryRemove(opaque, out var state))
                    {
                        state.Complete(response);
                    }
                    else
                    {
                        response.Dispose();

                        // create orphaned response context
                        // var context = CreateOperationContext(opaque);

                        // send to orphaned response reporter
                        //  ClusterOptions.ClientConfiguration.OrphanedResponseLogger.Add(context);
                    }
                }
                catch
                {
                    // Ownership of the buffer was not accepted by state.Complete due to an exception
                    // Make sure we release the buffer
                    response.Dispose();
                    throw;
                }

                UpdateLastActivity();
            }

            if (parsedOffset > 0)
            {
                if (parsedOffset < _receiveBufferLength)
                {
                    Buffer.BlockCopy(_receiveBuffer, parsedOffset, _receiveBuffer, 0, _receiveBufferLength-parsedOffset);
                }
                _receiveBufferLength -= parsedOffset;
            }
        }

        //our receive thread will always find out immediately when connection closes
        //we can easily catch the spots that mean connection was closed from the other
        //side and attach handling here
        private void HandleDisconnect(Exception exception)
        {
            //you might throw an event here to be handled by owner of this class, or can implement reconnect directly here etc...
            //Log.LogWarning("Handling disconnect for connection {0}: {1}", Identity, exception);

            //in any case the current socket object should be closed, all states in flight released etc.
            Close();
        }

        private DateTime _lastActivity = DateTime.UtcNow;

        /// <inheritdoc />
        public TimeSpan IdleTime => DateTime.UtcNow - _lastActivity;

        private void UpdateLastActivity()
        {
            _lastActivity = DateTime.UtcNow;
        }

        public void Close()
        {
            if (_disposed) return;
            lock (_syncObj)
            {
                _disposed = true;
                IsDead = true;

                if (Socket != null)
                {
                    try
                    {
                        if (Socket.Connected)
                        {
                            Socket.Shutdown(SocketShutdown.Both);
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation(e, string.Empty);
                    }
                    finally
                    {
                        Socket.Dispose();
                    }
                }

                // free up all states in flight
                lock (_statesInFlight)
                {
                    foreach (var state in _statesInFlight.Values)
                    {
                        state.Complete(null);
                        state.Dispose();
                    }
                }
            }
        }

        /// <inheritdoc />
        public async ValueTask CloseAsync(TimeSpan timeout)
        {
            if (_statesInFlight.Count == 0)
            {
                // Short circuit if nothing's in flight
                Close();
                return;
            }

            var allStatesTask = Task.WhenAll(
                _statesInFlight.Select(p => p.Value.CompletionTask));

            await Task.WhenAny(allStatesTask, Task.Delay(timeout)).ConfigureAwait(false);

            Close();
        }

        public void Dispose()
        {
            if (_disposed) return;
            Close();
        }
    }
}
