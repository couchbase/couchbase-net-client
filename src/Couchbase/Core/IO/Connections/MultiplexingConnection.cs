using System;
using System.Buffers;
using System.Collections.Concurrent;
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

namespace Couchbase.Core.IO.Connections
{
    public class MultiplexingConnection : IConnection
    {
        private readonly ConcurrentDictionary<uint, IState> _statesInFlight;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly Thread _receiveThread;
        private byte[] _receiveBuffer;
        private int _receiveBufferLength;
        private readonly object _syncObj = new object();
        protected volatile bool Disposed;
        protected ILogger Log;

        public MultiplexingConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter)
        {
            Socket = socket;
            Converter = converter;
            LocalEndPoint = socket.LocalEndPoint;
            EndPoint = socket.RemoteEndPoint;

            ConnectionPool = connectionPool;

            _statesInFlight = new ConcurrentDictionary<uint, IState>();

            //allocate a buffer
            _receiveBuffer = new byte[1024 * 16];
            _receiveBufferLength = 0;

            //Start a dedicated background thread for receiving server responses.
            _receiveThread = new Thread(ReceiveThreadBody);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }

        public ulong ConnectionId { get; }

        public IConnectionPool ConnectionPool { get; set; }

        public IByteConverter Converter { get; set; }

        public Socket Socket { get; set; }

        public bool IsConnected { get; }

        public EndPoint EndPoint { get; set; }

        EndPoint IConnection.LocalEndPoint { get; }

        public bool IsAuthenticated { get; set; }

        public bool IsSecure { get; }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is dead.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is dead; otherwise, <c>false</c>.
        /// </value>
        public bool IsDead { get; set; }

        public Task SendAsync(ReadOnlyMemory<byte> buffer, Func<SocketAsyncState, Task> callback)
        {
            return SendAsync(buffer, callback, null);
        }

        public Task SendAsync(ReadOnlyMemory<byte> request, Func<SocketAsyncState, Task> callback, ErrorMap errorMap)
        {
            var opaque = Converter.ToUInt32(request.Span.Slice(HeaderOffsets.Opaque));
            var state = new AsyncState
            {
                Opaque = opaque,
                Callback = callback,
                Converter = Converter,
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

            return Task.FromResult(0);
        }

        public bool InUse { get; private set; }

        public object LocalEndPoint { get; set; }

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
                var responseSize = Converter.ToInt32(_receiveBuffer.AsSpan(parsedOffset + HeaderOffsets.BodyLength)) + 24;
                if (parsedOffset + responseSize > _receiveBufferLength) break;

                var opaque = Converter.ToUInt32(_receiveBuffer.AsSpan(parsedOffset + HeaderOffsets.Opaque));
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

        /// <summary>
        /// Gets the timestamp of the last activity.
        /// </summary>
        public DateTime? LastActivity { get; private set; }

        protected void UpdateLastActivity()
        {
            LastActivity = DateTime.UtcNow;
        }

        /// <summary>
        /// Marks this <see cref="Connection"/> as used; meaning it cannot be disposed unless <see cref="InUse"/>
        /// is <c>false</c> or the <see cref="MaxCloseAttempts"/> has been reached.
        /// </summary>
        /// <param name="isUsed">if set to <c>true</c> [is used].</param>
        public void MarkUsed(bool isUsed)
        {
            InUse = isUsed;
        }

        public bool IsDisposed { get; private set; }
        public bool HasShutdown { get; private set; }
        public void Authenticate()
        {
            throw new NotImplementedException();
        }

        public bool CheckedForEnhancedAuthentication { get; set; }
        public bool MustEnableServerFeatures { get; set; }

        public void Close()
        {
            if (Disposed) return;
            lock (_syncObj)
            {
                Disposed = true;
                IsDead = true;
                MarkUsed(false);

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
                        Log.LogInformation(e, string.Empty);
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

        public void Dispose()
        {
            if (Disposed) return;
            Close();
        }
    }
}
