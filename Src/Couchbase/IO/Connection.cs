using System;
using System.Diagnostics;
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
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                Buffer = buffer
            };
            _eventArgs.UserToken = state;

            //set the buffer
            var bufferLength = buffer.Length < Configuration.BufferSize
                ? buffer.Length
                : Configuration.BufferSize;

            _eventArgs.SetBuffer(0, bufferLength);
            Buffer.BlockCopy(buffer, 0, _eventArgs.Buffer, 0, bufferLength);

            //Send the request
            if (!Socket.SendAsync(_eventArgs))
            {
                IsDead = true;
                throw new IOException("Failed to send operation!");
            }

            //wait for completion
            if (!_requestCompleted.WaitOne(Configuration.ConnectionTimeout))
            {
                IsDead = true;
                const string msg = "The connection has timed out while an operation was in flight. The default is 15000ms.";
                throw new IOException(msg);
            }

            //Check if an IO error occurred
            if (state.Exception != null)
            {
                IsDead = true;
                throw state.Exception;
            }

            //return the response bytes
            return state.Data.ToArray();
        }

        /// <summary>
        /// Raised when an asynchronous operation is completed
        /// </summary>
        /// <param name="sender">The <see cref="Socket"/> which the asynchronous operation is associated with.</param>
        /// <param name="args"></param>
        private void OnCompleted(object sender, SocketAsyncEventArgs args)
        {
            try
            {
                var socket = (Socket) sender;
                switch (args.LastOperation)
                {
                    case SocketAsyncOperation.Send:
                        Send(socket, args);
                        break;
                    case SocketAsyncOperation.Receive:
                        Receive(socket, args);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            catch (Exception e)
            {
                var state = args.UserToken as SocketAsyncState;
                if (state != null && state.Exception == null)
                {
                    state.Exception = e;
                }
                Log.Warn(e);
            }
        }

        /// <summary>
        /// Receives an asynchronous send operation
        /// </summary>
        /// <param name="socket">The <see cref="Socket"/> which the asynchronous operation is associated with.</param>
        /// <param name="e">The <see cref="SocketAsyncEventArgs"/> that is being used for the operation.</param>
        private void Send(Socket socket, SocketAsyncEventArgs e)
        {
            var state = (SocketAsyncState)e.UserToken;
            if (e.SocketError == SocketError.Success)
            {
                state.BytesSent += e.BytesTransferred;
                if (state.BytesSent < state.Buffer.Length)
                {
                    //set the buffer length to send, but don't exceed the saea buffer size
                    var bufferLength = state.Buffer.Length - state.BytesSent < Configuration.BufferSize
                        ? state.Buffer.Length - state.BytesSent
                        : Configuration.BufferSize;

                    //reset the saea buffer
                    _eventArgs.SetBuffer(0, bufferLength);

                    //copy and send the remaining portion of the buffer
                    Buffer.BlockCopy(state.Buffer, state.BytesSent, _eventArgs.Buffer, 0, bufferLength);
                    if (!Socket.SendAsync(_eventArgs))
                    {
                        OnCompleted(socket, e);
                    }
                }
                else
                {
                    var willRaiseCompletedEvent = socket.ReceiveAsync(e);
                    if (!willRaiseCompletedEvent)
                    {
                        OnCompleted(socket, e);
                    }
                }
            }
            else
            {
                IsDead = true;
                state.Exception = new SocketException((int) e.SocketError);
                _requestCompleted.Set();
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
                var state = (SocketAsyncState)e.UserToken;
                if (e.SocketError == SocketError.Success)
                {
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
                        var bufferSize = state.BodyLength < Configuration.BufferSize
                            ? state.BodyLength
                            : Configuration.BufferSize;

                        e.SetBuffer(0, bufferSize);
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
                    state.Exception = new SocketException((int)e.SocketError);
                    _requestCompleted.Set();
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

            try
            {
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
            catch (Exception e)
            {
                Log.Info(e);
            }
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