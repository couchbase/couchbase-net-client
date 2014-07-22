using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;
using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;

namespace Couchbase.IO.Strategies.Async
{
    /// <summary>
    /// A <see cref="IOStrategy"/> implementation for <see cref="SocketAsyncEventArgs"/>.
    /// </summary>
    internal sealed class SocketAsyncStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private readonly SocketAsyncPool _socketAsyncPool;
        private static readonly AutoResetEvent WaitEvent = new AutoResetEvent(true);
        private static readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        private ISaslMechanism _saslMechanism;
        private IByteConverter _converter;
        private volatile bool _disposed;

        public SocketAsyncStrategy(IConnectionPool connectionPool)
            : this(connectionPool,
            new SocketAsyncPool(connectionPool, SocketAsyncFactory.GetSocketAsyncFunc()),
            new PlainTextMechanism("default", string.Empty, new AutoByteConverter()), 
            new AutoByteConverter())
        {
        }

        public SocketAsyncStrategy(IConnectionPool connectionPool, ISaslMechanism saslMechanism)
            : this(connectionPool, new SocketAsyncPool(connectionPool, SocketAsyncFactory.GetSocketAsyncFunc()), saslMechanism, new AutoByteConverter())
        {
        }

        public SocketAsyncStrategy(IConnectionPool connectionPool, SocketAsyncPool socketAsyncPool)
        {
            _connectionPool = connectionPool;
            _socketAsyncPool = socketAsyncPool;
        }

        public SocketAsyncStrategy(IConnectionPool connectionPool, SocketAsyncPool socketAsyncPool, ISaslMechanism saslMechanism, IByteConverter converter)
        {
            _connectionPool = connectionPool;
            _socketAsyncPool = socketAsyncPool;
            _saslMechanism = saslMechanism;
            _saslMechanism.IOStrategy = this;
            _converter = converter;
        }

        /// <summary>
        /// Executes an <see cref="IOperation{T}"/> asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <param name="connection"></param>
        /// <returns></returns>
        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            var socketAsync = new SocketAsyncEventArgs
            {
                AcceptSocket = connection.Socket,
                UserToken = new OperationAsyncState
                {
                    Connection = connection
                }
            };
            socketAsync.Completed -= OnCompleted;
            socketAsync.Completed += OnCompleted;

            var state = (OperationAsyncState)socketAsync.UserToken;
            state.Reset();

            var socket = state.Connection.Socket;
            Log.Debug(m => m("sending key {0}", operation.Key));

            var buffer = operation.GetBuffer();
            socketAsync.SetBuffer(buffer, 0, buffer.Length);
            socket.SendAsync(socketAsync);
            SendEvent.WaitOne();//needs cancellation token timeout

            operation.Header = state.Header;
            operation.Body = state.Body;

            return operation.GetResult();
        }

        private void Authenticate(IConnection connection)
        {
            if (_saslMechanism != null)
            {
                var result = _saslMechanism.Authenticate(connection);
                if (result)
                {
                    connection.IsAuthenticated = true;
                }
                else
                {
                    throw new AuthenticationException(_saslMechanism.Username);
                }
            }
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var socketAsync = _socketAsyncPool.Acquire();
            WaitEvent.WaitOne();
            socketAsync.Completed -= OnCompleted;
            socketAsync.Completed += OnCompleted;

            var state = (OperationAsyncState)socketAsync.UserToken;
            state.Reset();

            try
            {
                var connection = state.Connection;
                if (!connection.IsAuthenticated)
                {
                    Authenticate(state.Connection);
                }

                var socket = state.Connection.Socket;
                Log.Debug(m => m("sending key {0} using {1}", operation.Key, state.Connection.Identity));

                var buffer = operation.GetBuffer();
                socketAsync.SetBuffer(buffer, 0, buffer.Length);
                socket.SendAsync(socketAsync);
                WaitEvent.Reset();
                SendEvent.WaitOne(); //needs cancellation token timeout

                operation.Header = state.Header;
                operation.Body = state.Body;
            }
            catch (AuthenticationException)
            {
                throw;
            }
            catch (Exception e)
            {
                operation.Exception = e;
                Log.Error(e);
            }
            finally
            {
                _socketAsyncPool.Release(socketAsync);
                WaitEvent.Set();
            }
            return operation.GetResult();
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    Receive(e);
                    break;

                case SocketAsyncOperation.Send:
                    Send(e);
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void Send(SocketAsyncEventArgs e)
        {
            Log.Debug(m=>m("send..."));
            if (e.SocketError == SocketError.Success)
            {
                var state = (OperationAsyncState)e.UserToken;
                var socket = state.Connection.Socket;

                var willRaiseCompletedEvent = socket.ReceiveAsync(e);
                if (!willRaiseCompletedEvent)
                {
                    Receive(e);
                }
            }
            else
            {
                throw new SocketException((int)e.SocketError);
            }
        }

        private void Receive(SocketAsyncEventArgs e)
        {
            while (true)
            {
                if (e.SocketError == SocketError.Success)
                {
                    var state = (OperationAsyncState)e.UserToken;
                    state.BytesReceived += e.BytesTransferred;
                    state.Data.Write(e.Buffer, e.Offset, e.Count);
                    Log.Debug(m => m("receive...{0} bytes of {1} offset {2}", state.BytesReceived, e.Count, e.Offset));

                    if (state.Header.BodyLength == 0)
                    {
                        CreateHeader(state);
                        Log.Debug(m => m("received key {0}", state.Header.Key));
                    }

                    if (state.BytesReceived < state.Header.TotalLength)
                    {
                        var willRaiseCompletedEvent = e.AcceptSocket.ReceiveAsync(e);
                        if (!willRaiseCompletedEvent)
                        {
                            continue;
                        }
                    }
                    else
                    {
                        Log.Debug(m => m("bytes rcvd/length: {0}/{1}", state.BytesReceived, state.Header.TotalLength));
                        CreateBody(state);
                        SendEvent.Set();
                    }
                }
                else
                {
                    throw new SocketException((int)e.SocketError);
                }
                break;
            }
        }

        private void CreateHeader(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Header = new OperationHeader
                {
                    Magic = _converter.ToByte(buffer, HeaderIndexFor.Magic),
                    OperationCode = _converter.ToByte(buffer, HeaderIndexFor.Opcode).ToOpCode(),
                    KeyLength = _converter.ToInt16(buffer, HeaderIndexFor.KeyLength),
                    ExtrasLength = _converter.ToByte(buffer, HeaderIndexFor.ExtrasLength),
                    Status = (ResponseStatus)_converter.ToInt16(buffer, HeaderIndexFor.Status),
                    BodyLength = _converter.ToInt32(buffer, HeaderIndexFor.Body),
                    Opaque = _converter.ToUInt32(buffer, HeaderIndexFor.Opaque),
                    Cas = _converter.ToUInt64(buffer, HeaderIndexFor.Cas)
                };
            }
        }

        private static void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            state.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength).Array,
                Data = new ArraySegment<byte>(buffer, 28, state.Header.BodyLength).Array,
            };
        }

        public ISaslMechanism SaslMechanism
        {
            set { _saslMechanism = value; }
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_connectionPool != null)
                {
                    _connectionPool.Dispose();
                }
                if (_socketAsyncPool != null)
                {
                    _socketAsyncPool.Dispose();
                }
                _disposed = true;
            }
        }

        ~SocketAsyncStrategy()
        {
            Dispose(false);
        }


        public bool IsSecure
        {
            get { throw new NotImplementedException(); }
        }
    }
}

#region [ License information          ]

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