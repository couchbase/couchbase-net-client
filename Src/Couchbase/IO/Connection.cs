using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

namespace Couchbase.IO
{
    internal class Connection : ConnectionBase
    {
        private readonly SocketAsyncEventArgs _eventArgs;
        private readonly AutoResetEvent _requestCompleted = new AutoResetEvent(false);
        private readonly BufferAllocator _allocator;
        private volatile bool _disposed;

        internal Connection(IConnectionPool connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new SocketAsyncEventArgs(), converter)
        {
        }

        internal Connection(IConnectionPool connectionPool, Socket socket, SocketAsyncEventArgs eventArgs, IByteConverter converter)
            : base(socket, converter)
        {
            //set the configuration info
            ConnectionPool = connectionPool;
            Configuration = ConnectionPool.Configuration;

            //Since the config can be changed on the fly create allocator late in the cycle
            _allocator = Configuration.BufferAllocator(Configuration);

            //create a seae with an accept socket and completed event
            _eventArgs = eventArgs;
            _eventArgs.AcceptSocket = socket;
            _eventArgs.Completed += OnCompleted;

            //set the buffer to use with this saea instance
            _allocator.SetBuffer(_eventArgs);
        }

        /// <summary>
        /// Sends a memcached operation as a buffer to a the server.
        /// </summary>
        /// <param name="buffer">A memcached request buffer</param>
        /// <returns>A memcached response buffer.</returns>
        public override byte[] Send(byte[] buffer)
        {
            //create the state object and set it
            var state = new SocketAsyncState
            {
                Data = new MemoryStream(),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque)
            };
            _eventArgs.UserToken = state;

            //set the buffer
            _eventArgs.SetBuffer(0, buffer.Length);
            Buffer.BlockCopy(buffer, 0, _eventArgs.Buffer, 0, buffer.Length);

            //Send the request
            if (!Socket.SendAsync(_eventArgs))
            {
                //TODO refactor logic
                IsDead = true;
                throw new IOException("Failed to send operation!");
            }

            //wait for completion
            if (!_requestCompleted.WaitOne(Configuration.ConnectionTimeout))
            {
                //TODO refactor logic
                IsDead = true;
                const string msg = "The connection has timed out while an operation was in flight. The default is 15000ms.";
                throw new IOException(msg);
            }

            //return the response bytes
            return state.Data.ToArray();
        }

        /// <summary>
        /// Raised when an asynchronous operation is completed
        /// </summary>
        /// <param name="sender">The <see cref="Socket"/> which the asynchronous operation is associated with.</param>
        /// <param name="e"></param>
        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            var socket = (Socket)sender;
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Send:
                    Send(socket, e);
                    break;
                case SocketAsyncOperation.Receive:
                    Receive(socket, e);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Receives an asynchronous send operation
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> which the asynchronous operation is associated with.</param>
        /// <param name="e">The <see cref="SocketAsyncEventArgs"/> that is being used for the operation.</param>
        private void Send(Socket socket, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _eventArgs.UserToken = e.UserToken;
                var willRaiseCompletedEvent = socket.ReceiveAsync(_eventArgs);
                if (!willRaiseCompletedEvent)
                {
                    OnCompleted(socket, e);
                }
            }
            else
            {
                throw new SocketException((int)e.SocketError);
            }
        }

        /// <summary>
        /// Recieves an asynchronous recieve operation and loops until the response body has been read.
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> which the asynchronous operation is associated with.</param>
        /// <param name="e">The <see cref="SocketAsyncEventArgs"/> that is being used for the operation.</param>
        public void Receive(Socket socket, SocketAsyncEventArgs e)
        {
            while (true)
            {
                if (e.SocketError == SocketError.Success)
                {
                    var state = (SocketAsyncState)e.UserToken;

                    //socket was closed on recieving side
                    if (e.BytesTransferred == 0)
                    {
                        _requestCompleted.Set();
                        return;
                    }
                    state.BytesReceived += e.BytesTransferred;
                    state.Data.Write(e.Buffer, 0, e.BytesTransferred);

                    //if first loop get the length of the body from the header
                    if (state.BodyLength == 0)
                    {
                        state.BodyLength = Converter.ToInt32(state.Data.GetBuffer(), HeaderIndexFor.Body);
                    }
                    if (state.BytesReceived < state.BodyLength + 24)
                    {
                        var willRaiseCompletedEvent = socket.ReceiveAsync(e);
                        if (!willRaiseCompletedEvent)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        _requestCompleted.Set();
                    }
                }
                else
                {
                    IsDead = true;
                    _requestCompleted.Set();
                    throw new SocketException((int)e.SocketError);
                }
                break;
            }
        }

        /// <summary>
        /// Diposes the underlying socket and other objects used by this instance.
        /// </summary>
        public override void Dispose()
        {
            IsDead = true;
            if (_disposed) return;

            _disposed = true;
            if (Socket != null)
            {
                if (Socket.Connected)
                {
                    Socket.Shutdown(SocketShutdown.Both);
                    Socket.Close(ConnectionPool.Configuration.ShutdownTimeout);
                }
                else
                {
                    Socket.Close();
                    Socket.Dispose();
                }
            }
            _allocator.ReleaseBuffer(_eventArgs);
            _eventArgs.Dispose();
            _requestCompleted.Dispose();
        }
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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