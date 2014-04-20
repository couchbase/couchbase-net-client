using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.Async
{
    internal class SocketAsyncStrategy : IOStrategy
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private readonly SocketAsyncPool _socketAsyncPool;
        private volatile bool _disposed;
        private static readonly AutoResetEvent WaitEvent= new AutoResetEvent(true);
        private static readonly AutoResetEvent SendEvent = new AutoResetEvent(false);

        public SocketAsyncStrategy(IConnectionPool connectionPool)
            : this(connectionPool, new SocketAsyncPool(connectionPool, SocketAsyncFactory.GetSocketAsyncFunc()))
        {
        }

        public SocketAsyncStrategy(IConnectionPool connectionPool, SocketAsyncPool socketAsyncPool)
        {
            _connectionPool = connectionPool;
            _socketAsyncPool = socketAsyncPool;
        }

        public Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            WaitEvent.WaitOne();
            var socketAsync = _socketAsyncPool.Acquire();
            socketAsync.Completed -= OnCompleted;
            socketAsync.Completed += OnCompleted;
           
            var state = (OperationAsyncState)socketAsync.UserToken;
            state.Reset();

            var socket = state.Connection.Socket;
            _log.Debug(m=>m("sending key {0}", operation.Key));

            var buffer = operation.GetBuffer();
            socketAsync.SetBuffer(buffer, 0, buffer.Length);
            socket.SendAsync(socketAsync);
            WaitEvent.Reset();    
            SendEvent.WaitOne();//needs cancellation token timeout
            
            operation.Header = state.Header;
            operation.Body = state.Body;

            _socketAsyncPool.Release(socketAsync);

            WaitEvent.Set();
            return operation.GetResult();
        }

        void OnCompleted(object sender, SocketAsyncEventArgs e)
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

        static void Send(SocketAsyncEventArgs e)
        {
            //_log.Debug(m=>m("send..."));
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

        private static void Receive(SocketAsyncEventArgs e)
        {
            while (true)
            {
                if (e.SocketError == SocketError.Success)
                {
                    var state = (OperationAsyncState) e.UserToken;
                    state.BytesReceived += e.BytesTransferred;
                    state.Data.Write(e.Buffer, e.Offset, e.Count);
                   // _log.Debug(m => m("receive...{0} bytes of {1} offset {2}", state.BytesReceived, e.Count, e.Offset));

                    if (state.Header.BodyLength == 0)
                    {
                        CreateHeader(state);
                        //_log.Debug(m => m("received key {0}", state.Header.Key));
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
                        //_log.Debug(m => m("bytes rcvd/length: {0}/{1}", state.BytesReceived, state.Header.TotalLength));
                        CreateBody(state);
                        SendEvent.Set();
                    }
                }
                else
                {
                    throw new SocketException((int) e.SocketError);
                }
                break;
            }
        }

        static void CreateHeader(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Header = new OperationHeader
                {
                    Magic = buffer[HeaderIndexFor.Magic],
                    OperationCode = buffer[HeaderIndexFor.Opcode].ToOpCode(),
                    KeyLength = buffer.GetInt16(HeaderIndexFor.KeyLength),
                    ExtrasLength = buffer[HeaderIndexFor.ExtrasLength],
                    Status = buffer.GetResponseStatus(HeaderIndexFor.Status),
                    BodyLength = buffer.GetInt32(HeaderIndexFor.Body),
                    Opaque = buffer.GetUInt32(HeaderIndexFor.Opaque),
                    Cas = buffer.GetUInt64(HeaderIndexFor.Cas)
                };
            }
        }

        static void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            state.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, 28, state.Header.BodyLength),
            };
        }

        public Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
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
            throw new NotImplementedException();
        }
    }
}
