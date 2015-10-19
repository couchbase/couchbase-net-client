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
    internal class Connection : ConnectionBase
    {
        private readonly SocketAsyncEventArgs _eventArgs;
        private readonly AutoResetEvent _requestCompleted = new AutoResetEvent(false);
        private readonly BufferAllocator _allocator;
        private Timer _asyncOperationTimer;
        private readonly object _asyncTimoutLock = new object();

        public Connection(IConnectionPool connectionPool, Socket socket, IByteConverter converter, BufferAllocator allocator)
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            Configuration = ConnectionPool.Configuration;

            //set the max close attempts so that a connection in use is not disposed
            MaxCloseAttempts = Configuration.MaxCloseAttempts;

            _allocator = allocator;

            //create a seae with an accept socket and completed event
            _eventArgs = new SocketAsyncEventArgs();
            _eventArgs.AcceptSocket = socket;
            _eventArgs.Completed += OnCompleted;

            //set the buffer to use with this saea instance
            _allocator.SetBuffer(_eventArgs);
        }

        public override void SendAsync(byte[] buffer, Func<SocketAsyncState, Task> callback)
        {
            SocketAsyncState state = null;
            try
            {
                state = BeginSend(buffer);
                
                // As this is going to be an async call we must register
                // the callback in the async state.
                state.Completed = callback;

                // Create a timer to monitor this operation. When the operation completes
                // it will be cancelled. If the operation doesn't complete before the send
                // timeout expires then it will be responsible for cancelling the request.
                _asyncOperationTimer = new Timer(
                    OnAsyncTimeoutCallback, state,Timeout.Infinite, Timeout.Infinite);

                // Start the timer going. This is done after the previous line to ensure
                // that _asyncOperationTimer is assigned to before the callback runs.
                _asyncOperationTimer.Change(Configuration.SendTimeout, Timeout.Infinite);
             
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
        /// Async Timer Callback
        /// 
        /// <para>
        /// This callback is responsible for marking connections as dead if an async
        /// operation times out. This is required to ensure that all operations
        /// complete and to make sure dead connections are removed from the pool.
        /// </para>
        /// </summary>
        /// <param name="state">The <see cref="SocketAsyncState"/> object for the operation which timed out.</param>
        private void OnAsyncTimeoutCallback(object state)
        {
            CancelTimerIfRunning();

            Socket.Close();

            var socketAsyncState = (SocketAsyncState)state;
            var timeoutException = CreateTimeoutException();

            Log.DebugFormat("Operation {0} timed out.", socketAsyncState.Opaque);

            MarkAsDead(socketAsyncState, timeoutException);
        }

        /// <summary>
        /// Cancel the Timer
        /// </summary>
        private void CancelTimerIfRunning()
        {
            if (_asyncOperationTimer != null)
            {
                _asyncOperationTimer.Dispose();
                _asyncOperationTimer = null;
            }
        }

        /// <summary>
        /// Begin a Send Operation
        /// <para>
        /// Creates an async socket state object and prepares the buffer for
        /// sending.
        /// </para>
        /// </summary>
        /// <param name="buffer">The buffer to send</param>
        /// <returns>Async state object ready to send.</returns>
        private SocketAsyncState BeginSend(byte[] buffer)
        {
            var state = new SocketAsyncState
            {
                Data = new MemoryStream(),
                Opaque = Converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                Buffer = buffer,
                SendOffset = _eventArgs.Offset
            };
            _eventArgs.UserToken = state;
            Log.Debug(m => m("Sending {0} with {1} on server {2}", state.Opaque, Identity, EndPoint));

            //set the buffer
            var bufferLength = buffer.Length < Configuration.BufferSize
                ? buffer.Length
                : Configuration.BufferSize;

            _eventArgs.SetBuffer(state.SendOffset, bufferLength);
            Buffer.BlockCopy(buffer, 0, _eventArgs.Buffer, state.SendOffset, bufferLength);
            return state;
        }

        /// <summary>
        /// Sends a memcached operation as a buffer to a the server.
        /// </summary>
        /// <param name="buffer">A memcached request buffer</param>
        /// <returns>A memcached response buffer.</returns>
        public override byte[] Send(byte[] buffer)
        {
            var state = BeginSend(buffer);

            //Send the request
            if (!Socket.SendAsync(_eventArgs))
            {
               OnCompleted(Socket, _eventArgs);
            }

            //wait for completion
            if (!_requestCompleted.WaitOne(Configuration.SendTimeout))
            {
                IsDead = true;
                throw CreateTimeoutException();
            }

            //Check if an IO error occurred
            if (state.Exception != null)
            {
                Log.DebugFormat("Connection {0} has failed with {1}", Identity, state.Exception);
                IsDead = true;
                throw state.Exception;
            }

            Log.DebugFormat("Complete opaque{0} on {1}", state.Opaque, Identity);
            //return the response bytes
            return state.Data.ToArray();
        }

        /// <summary>
        /// Creates a new RemoteHostTimeoutException
        /// </summary>
        /// <returns>The new <see cref="RemoteHostTimeoutException"/> object.</returns>
        private static RemoteHostTimeoutException CreateTimeoutException()
        {
            return new RemoteHostTimeoutException(
                "The connection has timed out while an operation was in " +
                "flight. The default is 15000ms.");
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

            if (e.SocketError != SocketError.Success)
            {
                MarkAsDead(state, (int)e.SocketError);
                return;
            }

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

                bool sentAsync;
                lock (_asyncTimoutLock)
                {
                    if (IsDead)
                        return;
                    sentAsync = Socket.SendAsync(_eventArgs);
                }

                if (!sentAsync)
                {
                    OnCompleted(socket, e);
                }
            }
            else
            {
                e.SetBuffer(state.SendOffset, Configuration.BufferSize);
                bool recvdAsync;
                lock (_asyncTimoutLock)
                {
                    if (IsDead)
                        return;

                    recvdAsync = socket.ReceiveAsync(e);
                }

                if (!recvdAsync)
                {
                    OnCompleted(socket, e);
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
            while (DoReceive(socket, e))
            {
                // Keep on looping while there is more data to
                // recieve.
            }
        }
        
        /// <summary>
        /// Performs an Async Recieve Step
        /// <para>
        /// Completes an ansyc receive operation on a socket and returns a
        /// status bool to indicate if more data is ready to be recieved.
        /// </para>
        /// </summary>
        /// <param name="socket">The socket which the operation is taking place on.</param>
        /// <param name="e">The SAEA for the current operation.</param>
        /// <returns>True if there is more data to receive, fals otherwise</returns>
        private bool DoReceive(Socket socket, SocketAsyncEventArgs e)
        {
            var state = (SocketAsyncState)e.UserToken;
            Log.Debug(m => m("Receive {0} bytes for opaque{1} with {2} on server {3} offset{4}", e.BytesTransferred, state.Opaque, Identity, EndPoint, state.SendOffset));

            // If there is a problem with the socket then abort this operation and
            // mark as dead so the connection is removed from the pool.
            if (e.SocketError != SocketError.Success)
            {
                MarkAsDead(state, (int)e.SocketError);
                return false;
            }

            //socket was closed on recieving side
            if (e.BytesTransferred == 0)
            {
                Log.DebugFormat("Connection {0} has failed in receive with {1} bytes.", Identity,
                    e.BytesTransferred);
                MarkAsDead(state, 10054);
                return false;
            }

            state.BytesReceived += e.BytesTransferred;
            state.Data.Write(e.Buffer, state.SendOffset, e.BytesTransferred);

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

                e.SetBuffer(state.SendOffset, bufferSize);

                bool recvdAsync;
                lock (_asyncTimoutLock)
                {
                    if (IsDead)
                        return false;

                    recvdAsync = socket.ReceiveAsync(e);
                }

                // Rather than calling OnCompleted again here
                // we return true to let `Recieve` know we are
                // ready for another recieve event.
                return !recvdAsync;
            }

            Log.Debug(
                m => m("Complete {0} with {1} on server {2}",
                    state.Opaque, Identity, EndPoint));

            SetCompleted(state);
            return false;
        }

        /// <summary>
        /// Set an Async Operation as Completed
        /// </summary>
        /// <param name="state">The state for the operation to complete</param>
        private void SetCompleted(SocketAsyncState state)
        {
            CancelTimerIfRunning();

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

        /// <summary>
        /// Marks the current connection as dead
        /// 
        /// <para>
        /// Ends the current request with a socket error. This is an overload
        /// of <see cref="MarkAsDead(SocketAsyncState, Exception)"/>.
        /// </para>
        /// </summary>
        /// <param name="state">The state of the current operation</param>
        /// <param name="error">The socket exception error number</param>
        private void MarkAsDead(SocketAsyncState state, int error)
        {
            var ex = new SocketException(error);
            MarkAsDead(state, ex);
        }

        /// <summary>
        /// Marks the current connection as dead
        /// 
        /// <para>
        /// Ends the current request with an error, and marks the connection as dead
        /// so it can be removed from the conneciton pool.</para>
        /// </summary>
        /// <param name="state">The state of the current operation</param>
        /// <param name="exception">The exception to set on the operation state.</param>
        private void MarkAsDead(SocketAsyncState state, Exception exception)
        {
            lock (_asyncTimoutLock)
            {
                IsDead = true;

                state.Exception = exception;

                Log.Debug(m => m("Error: {0} - {1}", Identity, state.Exception));

                SetCompleted(state);
            }
        }

        /// <summary>
        /// Disposes the underlying socket and other objects used by this instance.
        /// </summary>
        public override void Dispose()
        {
            Log.DebugFormat("Disposing {0}", _identity);
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
                        Socket.Close(ConnectionPool.Configuration.ShutdownTimeout);
                    }
                    else
                    {
                        Socket.Close();
                        Socket.Dispose();
                    }
                }

                //call the bases dispose to cleanup the timer
                base.Dispose();

                _allocator.ReleaseBuffer(_eventArgs);
                _eventArgs.Dispose();
                _requestCompleted.Dispose();

                CancelTimerIfRunning();
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
