using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies
{
    internal sealed class EapConnection : ConnectionBase
    {
        private readonly ConnectionPool<EapConnection> _connectionPool;
        private readonly NetworkStream _networkStream ;
        private readonly AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private volatile bool _disposed;

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, IByteConverter converter) 
            : this(connectionPool, socket, new NetworkStream(socket), converter)
        {
        }

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, NetworkStream networkStream, IByteConverter converter) 
            : base(socket, converter)
        {
            _connectionPool = connectionPool;
            _networkStream = networkStream;
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            try
            {
                operation.Reset();
                var buffer = operation.Write();

                _networkStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, operation);
                _sendEvent.WaitOne();
            }
            catch (IOException e)
            {
                Log.Warn(e);
                WriteError("Failed. Check Exception property.", operation, 0);
                operation.Exception = e;
                _sendEvent.Set();
            }

            return operation.GetResult();
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            var operation = (IOperation)asyncResult.AsyncState;
            try
            {
                _networkStream.EndWrite(asyncResult);
                operation.Buffer = BufferManager.TakeBuffer(512);
                _networkStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
            }
            catch (IOException e)
            {
                Log.Warn(e);
                WriteError("Failed. Check Exception property.", operation, 0);
                operation.Exception = e;
                _sendEvent.Set();
            }
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var operation = (IOperation) asyncResult.AsyncState;

            try
            {
                var bytesRead = _networkStream.EndRead(asyncResult);
                operation.Read(operation.Buffer, 0, bytesRead);
                BufferManager.ReturnBuffer(operation.Buffer);

                if (operation.LengthReceived < operation.TotalLength)
                {
                    operation.Buffer = BufferManager.TakeBuffer(512);
                    _networkStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
                }
                else
                {
                    _sendEvent.Set();
                }
            }
            catch (IOException e)
            {
                Log.Warn(e);
                WriteError("Failed. Check Exception property.", operation, 0);
                operation.Exception = e;
                _sendEvent.Set();
            }
        }

        static void WriteError(string errorMsg, IOperation operation, int offset)
        {
            var bytes = Encoding.UTF8.GetBytes(errorMsg);
            operation.Read(bytes, offset, errorMsg.Length);
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!_disposed)
                {
                    if (Socket != null)
                    {
                        if (Socket.Connected)
                        {
                            Socket.Shutdown(SocketShutdown.Both);
                            Socket.Close(_connectionPool.Configuration.ShutdownTimeout);
                        }
                        else
                        {
                            Socket.Close();
                            Socket.Dispose();
                        }
                    }
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
                    }
                }
            }
            else
            {
                if (Socket != null)
                {
                    Socket.Close();
                    Socket.Dispose();
                }
                if (_networkStream != null)
                {
                    _networkStream.Dispose();
                }
            }
            _disposed = true;
        }

        ~EapConnection()
        {
            Dispose(false);
        }
    }
}
