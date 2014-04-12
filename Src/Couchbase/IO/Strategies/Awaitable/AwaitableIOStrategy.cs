using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Server.Providers;
using Couchbase.IO.Operations;
using Couchbase.IO.Operations.Authentication;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.Awaitable
{
    internal class AwaitableIOStrategy : IOStrategy
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly IConnectionPool _connectionPool;
        private readonly SocketAsyncEventArgsPool _asyncEventArgsPool;
        private volatile bool _disposed;
                                        
        public AwaitableIOStrategy(IConnectionPool connectionPool, SocketAsyncEventArgsPool asyncEventArgsPool)
        {
            _connectionPool = connectionPool;
            _asyncEventArgsPool = asyncEventArgsPool;
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            await Send(operation);
            return operation.GetResult();
        }

        public async Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation, IConnection connection)
        {
            await Send(operation, new OperationAsyncState
            {
                Connection = connection,
                OperationId = operation.SequenceId
            });
            return operation.GetResult();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            Send(operation);
            return operation.GetResult();
        }

        async Task Send<T>(IOperation<T> operation, OperationAsyncState operationAsyncState)
        {
            var buffer = operation.GetBuffer();
            var receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.UserToken = operationAsyncState;
            receiveEventArgs.SetBuffer(buffer, 0, buffer.Length);

            Log.Debug(m => m("Send Thread: {0} socket: {1}", Thread.CurrentThread.ManagedThreadId, operationAsyncState.Connection.Socket.Handle));

            var awaitable = new SocketAwaitable(receiveEventArgs);
            await operationAsyncState.Connection.Socket.SendAsync(awaitable);
            await Receive(operation, operationAsyncState);
        }

        async Task Send<T>(IOperation<T> operation)
        {
            var connection = _connectionPool.Acquire();

            await Send(operation, new OperationAsyncState
            {
                Connection = connection,
                OperationId = operation.SequenceId
            });

            _connectionPool.Release(connection);
        }

        async Task Receive<T>(IOperation<T> operation, OperationAsyncState state)
        {
            var args = new SocketAsyncEventArgs();
            args.SetBuffer(state.Buff, 0, state.Buff.Length);
            var awaitable = new SocketAwaitable(args);

            do
            {
                Log.Debug(m => m("Receive Thread: {0} socket: {1}", Thread.CurrentThread.ManagedThreadId, state.Connection.Socket.Handle));
                
                await state.Connection.Socket.ReceiveAsync(awaitable);
                state.BytesSent += args.BytesTransferred;
                state.Data.Write(state.Buff, 0,  args.BytesTransferred);
                args.SetBuffer(state.Buff, 0, state.Buff.Length);

                if (operation.Header.BodyLength== 0)
                {
                    CreateHeader(operation, state);
                }
            } 
            while (state.BytesSent < operation.Header.TotalLength);

            CreateBody(operation, state);
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
    }
}
