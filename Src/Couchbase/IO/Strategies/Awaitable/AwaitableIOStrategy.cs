using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.Awaitable
{
    internal sealed class AwaitableIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private readonly AwaitableSocketPool _awaitableSocketPool;
        private volatile bool _disposed;

        public AwaitableIOStrategy(IConnectionPool connectionPool) 
            : this(connectionPool, new AwaitableSocketPool(connectionPool, AwaitableSocketFactory.GetSocketAwaitable()))
        {
        }
       
        public AwaitableIOStrategy(IConnectionPool connectionPool, AwaitableSocketPool awaitableSocketPool)
        {
            _connectionPool = connectionPool;
            _awaitableSocketPool = awaitableSocketPool;
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            var socketAwaitable = _awaitableSocketPool.Acquire();
            var socketAsync = socketAwaitable.EventArgs;

            var buffer = operation.GetBuffer();
            Log.Debug(m => m("writing buffer...{0} bytes", buffer.Length));

             socketAsync.SetBuffer(buffer, 0, buffer.Length);

            Log.Debug(m => m("sending buffer...{0} bytes", buffer.Length));
 
            await socketAwaitable.SendAsync();
            await Receive(operation, socketAwaitable);

            Log.Debug(m => m("sent buffer...{0} bytes", buffer.Length));
            _awaitableSocketPool.Release(socketAwaitable);
            return operation.GetResult();
        }       

        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            var eventArgs = new SocketAsyncEventArgs
            {
                AcceptSocket = connection.Socket,
                UserToken = new OperationAsyncState
                {
                    Connection = connection
                }
            };
            var socketAwaitable = new SocketAwaitable(eventArgs);
            
            var buffer = operation.GetBuffer();
            socketAwaitable.EventArgs.SetBuffer(buffer, 0, buffer.Length);

            await socketAwaitable.SendAsync();
            await Receive(operation, socketAwaitable);
            return operation.GetResult();
        }

        private async Task Receive<T>(IOperation<T> operation, SocketAwaitable socketAwaitable)
        {
            var eventArgs = socketAwaitable.EventArgs;
            var state = (OperationAsyncState)eventArgs.UserToken;
            socketAwaitable.EventArgs.SetBuffer(state.Buffer, 0, state.Buffer.Length);
            
            do
            {
                await socketAwaitable.ReceiveAsync();
                state.BytesReceived += eventArgs.BytesTransferred;
                state.Data.Write(eventArgs.Buffer, eventArgs.Offset, eventArgs.Count);
                Log.Debug(m => m("receive...{0} bytes", state.BytesReceived));

                if (operation.Header.BodyLength == 0)
                {
                    CreateHeader(operation, state);
                }
            } 
            while (state.BytesReceived < operation.Header.TotalLength);

            CreateBody(operation, state);
            state.Reset();
        }

        static void CreateHeader<T>(IOperation<T> operation, OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                operation.Header = new OperationHeader
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

        static void CreateBody<T>(IOperation<T> operation, OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            operation.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<T>.HeaderLength, operation.Header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, 28, operation.Header.BodyLength),
            };
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
                _connectionPool.Dispose();
                _disposed = true;
            }
        }

        ~AwaitableIOStrategy()
        {
            Dispose(false);
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }


        public Authentication.SASL.ISaslMechanism SaslMechanism
        {
            set { throw new NotImplementedException(); }
        }
    }
}
