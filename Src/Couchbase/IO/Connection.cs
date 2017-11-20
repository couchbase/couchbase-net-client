using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;
using Couchbase.Utils;

namespace Couchbase.IO
{
    public class Connection : ConnectionBase
    {
        private readonly SocketAsyncEventArgs _eventArgs;
        private readonly AutoResetEvent _requestCompleted = new AutoResetEvent(false);

        public Connection(IConnectionPool connectionPool, Socket socket, IByteConverter converter, BufferAllocator allocator)
            : base(socket, converter, allocator)
        {
            ConnectionPool = connectionPool;
            Configuration = ConnectionPool.Configuration;

            //set the max close attempts so that a connection in use is not disposed
            MaxCloseAttempts = Configuration.MaxCloseAttempts;

            //create a seae with an accept socket and completed event
            _eventArgs = new SocketAsyncEventArgs();
            _eventArgs.AcceptSocket = socket;
            _eventArgs.Completed += OnCompleted;

            //set the buffer to use with this saea instance
            if (!BufferAllocator.SetBuffer(_eventArgs))
            {
                // failed to acquire a buffer because the allocator was exhausted

                throw new BufferUnavailableException("Unable to allocate a buffer for this connection because the BufferAllocator is exhausted.");
            }
        }

        public override void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback)
        {
            SocketAsyncState state = null;
            try
            {
                state = new SocketAsyncState
                {
                    Data = MemoryStreamFactory.GetMemoryStream(),
                    Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Buffer = buffer,
                    Completed = callback,
                    SendOffset = _eventArgs.Offset
                };

                _eventArgs.UserToken = state;
                Log.Debug("Sending {0} with {1} on server {2}", state.Opaque, Identity, EndPoint);

                //set the buffer
                var bufferLength = buffer.Length < Configuration.BufferSize
                    ? buffer.Length
                    : Configuration.BufferSize;

                _eventArgs.SetBuffer(state.SendOffset, bufferLength);
                Buffer.BlockCopy(buffer, 0, _eventArgs.Buffer, state.SendOffset, bufferLength);

                //Send the request
                if (!Socket.SendAsync(_eventArgs))
                {
                    OnCompleted(Socket, _eventArgs);
                }
            }
            catch (Exception e)
            {
                if (state == null)
                {
                    callback(new SocketAsyncState
                    {
                        Exception = e,
                        Status = (e is SocketException) ?
                            ResponseStatus.TransportFailure :
                            ResponseStatus.ClientFailure
                    });
                }
                else
                {
                    state.Exception = e;
                    state.Completed(state);
                    Log.Debug(e);
                }
            }
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
                Data = MemoryStreamFactory.GetMemoryStream(),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                Buffer = buffer,
                SendOffset = _eventArgs.Offset
            };

            Log.Debug("Sending opaque{0} on {1}", state.Opaque, Identity);
            _eventArgs.UserToken = state;

            //set the buffer
            var bufferLength = buffer.Length < Configuration.BufferSize
                ? buffer.Length
                : Configuration.BufferSize;

            _eventArgs.SetBuffer(state.SendOffset, bufferLength);
            Buffer.BlockCopy(buffer, 0, _eventArgs.Buffer, state.SendOffset, bufferLength);

            //Send the request
            if (!Socket.SendAsync(_eventArgs))
            {
               OnCompleted(Socket, _eventArgs);
            }

            //wait for completion
            if (!_requestCompleted.WaitOne(Configuration.SendTimeout))
            {
                IsDead = true;
                var msg = ExceptionUtil.GetMessage(ExceptionUtil.RemoteHostTimeoutMsg, Configuration.SendTimeout);
                throw new RemoteHostTimeoutException(msg);
            }

            //Check if an IO error occurred
            if (state.Exception != null)
            {
                Log.Debug("Connection {0} has failed with {1}", Identity, state.Exception);
                IsDead = true;
                throw state.Exception;
            }

            Log.Debug("Complete opaque{0} on {1}", state.Opaque, Identity);
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
                        throw new ArgumentOutOfRangeException(args.LastOperation.ToString());
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
                    _eventArgs.SetBuffer(state.SendOffset, bufferLength);

                    //copy and send the remaining portion of the buffer
                    Buffer.BlockCopy(state.Buffer, state.BytesSent, _eventArgs.Buffer, state.SendOffset, bufferLength);
                    if (!Socket.SendAsync(_eventArgs))
                    {
                        OnCompleted(socket, e);
                    }
                }
                else
                {
                    e.SetBuffer(state.SendOffset, Configuration.BufferSize);
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
                Log.Debug("Error: {0} - {1}", Identity, state.Exception);
                //if the callback is null we are in blocking mode
                if (state.Completed == null)
                {
                    _requestCompleted.Set();
                }
                else
                {
                    ConnectionPool.Release(this);
                    state.Completed(state);
                }
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
                Log.Debug("Receive {0} bytes for opaque{1} with {2} on server {3} offset{4}", e.BytesTransferred, state.Opaque, Identity, EndPoint, state.SendOffset);
                if (e.SocketError == SocketError.Success)
                {
                    //socket was closed on recieving side
                    if (e.BytesTransferred == 0)
                    {
                        Log.Debug("Connection {0} has failed in receive with {1} bytes.", Identity, e.BytesTransferred);
                        IsDead = true;
                        if (state.Completed == null)
                        {
                            if (!Disposed)
                            {
                                _requestCompleted.Set();
                            }
                        }
                        else
                        {
                            ConnectionPool.Release(this);
                            state.Exception = new SocketException(10054);
                            state.Completed(state);
                        }
                        break;
                    }
                    state.BytesReceived += e.BytesTransferred;
                    state.Data.Write(e.Buffer, state.SendOffset, e.BytesTransferred);

                    //if first loop get the length of the body from the header
                    if (state.BodyLength == 0)
                    {
                        state.BodyLength = Converter.ToInt32(state.Data.ToArray(), HeaderIndexFor.Body);
                    }
                    if (state.BytesReceived < state.BodyLength + 24)
                    {
                        var bufferSize = state.BodyLength < Configuration.BufferSize
                            ? state.BodyLength
                            : Configuration.BufferSize;

                        e.SetBuffer(state.SendOffset, bufferSize);
                        var willRaiseCompletedEvent = socket.ReceiveAsync(e);
                        if (!willRaiseCompletedEvent)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        //if the callback is null we are in blocking mode
                        if (state.Completed == null)
                        {
                            Log.Debug("Complete with set {0} with {1} on server {2}", state.Opaque, Identity, EndPoint);
                            _requestCompleted.Set();
                        }
                        else
                        {
                            Log.Debug("Complete {0} with {1} on server {2}", state.Opaque, Identity, EndPoint);
                            ConnectionPool.Release(this);
                            state.Completed(state);
                        }
                    }
                }
                else
                {
                    IsDead = true;
                    state.Exception = new SocketException((int)e.SocketError);
                    Log.Debug("Error: {0} - {1}", Identity, state.Exception);
                    //if the callback is null we are in blocking mode
                    if (state.Completed == null)
                    {
                        if (!Disposed)
                        {
                            _requestCompleted.Set();
                        }
                    }
                    else
                    {
                        ConnectionPool.Release(this);
                        state.Completed(state);
                    }
                }
                break;
            }

            UpdateLastActivity();
        }

#if DEBUG
        /// <summary>
        /// Cleans up any non-reclaimed resources.
        /// </summary>
        /// <remarks>will run if Dispose is not called on a Connection instance.</remarks>
        ~Connection()
        {
            Dispose();
            Log.Debug("Finalizing {0}", GetType().Name);
        }
#endif

        /// <summary>
        /// Disposes the underlying socket and other objects used by this instance.
        /// </summary>
        public override void Dispose()
        {
            Log.Debug("Disposing {0}", _identity);
            if (Disposed || InUse && !IsDead) return;
            Disposed = true;
            IsDead = true;

            try
            {
                if (Socket != null)
                {
                    if (Socket.Connected)
                    {
                        Socket.Shutdown(SocketShutdown.Both);
                    }

                    Socket.Dispose();
                }
                //call the bases dispose to cleanup the timer
                base.Dispose();

                _eventArgs.Dispose();
                _requestCompleted.Dispose();
            }
            catch (Exception e)
            {
                Log.Info(e);
            }

            try
            {
                // Release the buffer in a separate try..catch block, because we want to ensure this happens
                // even if other steps fail.  Otherwise we will run out of buffers when the ConnectionPool reaches
                // its maximum size.

                BufferAllocator.ReleaseBuffer(_eventArgs);
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
