using System;
using System.Net;
using System.Net.Sockets;
using Couchbase.IO.Operations;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.Blocking
{
    internal class BlockingIOStrategy : IOStrategy
    {
        private readonly IConnectionPool _connectionPool;
        private volatile bool _disposed;

        public BlockingIOStrategy(IConnectionPool connectionPool)
        {
            _connectionPool = connectionPool;
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            IConnection connection = null;

            try
            {
                var buffer = operation.CreateBuffer();
                connection = _connectionPool.Acquire();

                SocketError error;
                connection.Handle.Send(buffer, SocketFlags.None, out error);

                operation.Header = ReadHeader(connection);
                if (operation.Header.HasData())
                {
                    operation.Body = ReadBody(connection, operation.Header);
                }
            }
            finally
            {
                _connectionPool.Release(connection);
            }

            return operation.GetResult();
        }

        OperationHeader ReadHeader(IConnection connection)
        {
            var header = new ArraySegment<byte>(new byte[24]);
            connection.Handle.Receive(header.Array, 0, header.Array.Length, SocketFlags.None);

            byte[] buffer = header.Array;
            return new OperationHeader
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

        OperationBody ReadBody(IConnection connection, OperationHeader header)
        {
            var buffer = new byte[header.BodyLength];
            connection.Handle.Receive(buffer, 0, buffer.Length, SocketFlags.None);

            return new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, 0, header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, header.ExtrasLength, buffer.Length - header.BodyLength),
            };
        }


        public System.Threading.Tasks.Task<IOperationResult<T>> ExecuteAsync<T>(IOperation<T> operation)
        {
            throw new NotImplementedException();
        }

        public void RegisterListener(Configuration.Server.Providers.IConfigListener listener)
        {
            throw new NotImplementedException();
        }

        public void UnRegisterListener(Configuration.Server.Providers.IConfigListener listener)
        {
            throw new NotImplementedException();
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public IPEndPoint EndPoint
        {
            get { return _connectionPool.EndPoint; }
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

        ~BlockingIOStrategy()
        {
            Dispose(false);
        }
    }
}
