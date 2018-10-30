using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Monitoring;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Errors;
using Couchbase.IO.Utils;
using Couchbase.Tracing;
using Couchbase.Utils;
using OpenTracing;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a connection for pipelining Memcached requests/responses to and from a server.
    /// </summary>
    public class MultiplexingConnection : ConnectionBase
    {
        private readonly ConcurrentDictionary<uint, IState> _statesInFlight;
        private readonly ConcurrentQueue<SyncState> _statePool;
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        private readonly Thread _receiveThread;
        private byte[] _receiveBuffer;
        private int _receiveBufferLength;
        private readonly object _syncObj = new object();

        public MultiplexingConnection(IConnectionPool connectionPool, Socket socket, IByteConverter converter,
            BufferAllocator allocator)
            : base(socket, converter, allocator)
        {
            ConnectionPool = connectionPool;
            Configuration = ConnectionPool.Configuration;

            //set the max close attempts so that a connection in use is not disposed
            MaxCloseAttempts = Configuration.MaxCloseAttempts;

            _statesInFlight = new ConcurrentDictionary<uint, IState>();
            _statePool = new ConcurrentQueue<SyncState>();

            //allocate a buffer
            _receiveBuffer = new byte[Configuration.BufferSize];
            _receiveBufferLength = 0;

            //Start a dedicated background thread for receiving server responses.
            _receiveThread = new Thread(ReceiveThreadBody);
            _receiveThread.IsBackground = true;
            _receiveThread.Start();
        }

        public override Task SendAsync(byte[] request, Func<SocketAsyncState, Task> callback, ISpan span, ErrorMap errorMap)
        {
            var opaque = Converter.ToUInt32(request, HeaderIndexFor.Opaque);
            var state = new AsyncState
            {
                Opaque = opaque,
                Callback = callback,
                Converter = Converter,
                EndPoint = (IPEndPoint)EndPoint,
                DispatchSpan = span,
                ConnectionId = ContextId,
                ErrorMap = errorMap,
                Timeout = Configuration.SendTimeout,
                LocalEndpoint = LocalEndPoint.ToString()
            };

            _statesInFlight.TryAdd(state.Opaque, state);

            state.Timer = new Timer(o =>
            {
                AsyncState a = (AsyncState)o;
                _statesInFlight.TryRemove(a.Opaque, out _);
                a.Cancel(ResponseStatus.OperationTimeout, CreateTimeoutException(opaque));
            }, state, Configuration.SendTimeout, Timeout.Infinite);

            var sentBytesCount = 0;
            lock (Socket)
            {
                try
                {
                    do
                    {
                        sentBytesCount += Socket.Send(request, sentBytesCount, request.Length - sentBytesCount,
                            SocketFlags.None);

                    } while (sentBytesCount < request.Length);
                }
                catch (Exception e)
                {
                    HandleDisconnect(e);
                }
            }

            return Task.FromResult(0);
        }

        /// <summary>
        /// Sends a Memcached packet to the server and waits for a response.
        /// </summary>
        /// <param name="request">The memcached request packet.</param>
        /// <returns></returns>
        /// <exception cref="SendTimeoutExpiredException"></exception>
        public override byte[] Send(byte[] request)
        {
            var state = AcquireState();
            var opaque = Converter.ToUInt32(request, HeaderIndexFor.Opaque);

            _statesInFlight.TryAdd(opaque, state);

            var sentBytesCount = 0;
            lock (Socket)
            {
                try
                {
                    do
                    {
                        sentBytesCount += Socket.Send(request, sentBytesCount, request.Length - sentBytesCount,
                            SocketFlags.None);

                    } while (sentBytesCount < request.Length);
                }
                catch (Exception e)
                {
                    HandleDisconnect(e);
                }
            }

            var didComplete = state.SyncWait.WaitOne(Configuration.SendTimeout);
            var response = state.Response;

            _statesInFlight.TryRemove(opaque, out _);

            ReleaseState(state);

            if (!didComplete)
            {
                throw CreateTimeoutException(opaque);
            }

            return response;
        }

        /// <summary>
        /// Gets a <see cref="SyncState"/> object if one exists in the pool or creates and returns a new one.
        /// </summary>
        /// <returns>An <see cref="SyncState"/> object representing the state of the request.</returns>
        private SyncState AcquireState()
        {
            if (_statePool.TryDequeue(out var state))
            {
                return state;
            }
            return new SyncState();
        }

        /// <summary>
        /// Releases a <see cref="SyncState"/> object back into the pool for reuse.
        /// </summary>
        /// <param name="state">The state.</param>
        private void ReleaseState(SyncState state)
        {
            state.CleanForReuse();
            _statePool.Enqueue(state);
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
                HandleDisconnect(new RemoteHostClosedException(
                    ExceptionUtil.GetMessage(ExceptionUtil.RemoteHostClosedMsg, EndPoint)));
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
            while (parsedOffset + HeaderIndexFor.BodyLength < _receiveBufferLength)
            {
                var responseSize = Converter.ToInt32(_receiveBuffer, parsedOffset + HeaderIndexFor.BodyLength) + OperationHeader.Length;
                if (parsedOffset + responseSize > _receiveBufferLength) break;

                var opaque = Converter.ToUInt32(_receiveBuffer, parsedOffset + HeaderIndexFor.Opaque);
                var response = new byte[responseSize];
                Buffer.BlockCopy(_receiveBuffer, parsedOffset, response, 0, responseSize);

                parsedOffset += responseSize;

                if (_statesInFlight.TryRemove(opaque, out var state))
                {
                    state.Complete(response);
                }
                else
                {
                    // create orphaned response context
                    var context = CreateOperationContext(opaque);

                    // send to orphaned response reporter
                    Configuration.ClientConfiguration.OrphanedResponseLogger.Add(context);
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
            //ignore a disposed exception - it means we've already been reclaimed - NCBC-1785
            if (!(exception is ObjectDisposedException))
            {
                //you might throw an event here to be handled by owner of this class, or can implement reconnect directly here etc...
                // ReSharper disable once InconsistentlySynchronizedField
                Log.Warn("Handling disconnect for connection {0}: {1}", _identity, exception);
            }
            //in any case the current socket object should be closed, all states in flight released etc.
            Close();
        }

        public void Close()
        {
            if (Disposed) return;
            lock (_syncObj)
            {
                Log.Info("Closing connection {0}", Identity);
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
                        Log.Info(e);
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

                // clean up SyncState pool
                lock (_statePool)
                {
                    while (_statePool.TryDequeue(out var state))
                    {
                        state.Dispose();
                    }
                }
            }
        }

        public override void Dispose()
        {
            if (Disposed) return;
            Log.Debug("Disposing {0}", _identity);
            Close();
        }
    }
}


#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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

#endregion
