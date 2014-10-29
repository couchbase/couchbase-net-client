using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using System;
using System.Net.Sockets;

namespace Couchbase.IO.Strategies
{
    internal sealed class EapConnection : ConnectionBase
    {
        private readonly NetworkStream _networkStream;

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, IByteConverter converter)
            : this(connectionPool, socket, new NetworkStream(socket), converter)
        {
        }

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, NetworkStream networkStream, IByteConverter converter)
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            _networkStream = networkStream;
            Configuration = ConnectionPool.Configuration;
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            try
            {
                var buffer = operation.Write();
                _networkStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, operation);
                if (!SendEvent.WaitOne(Configuration.ConnectionTimeout))
                {
                    const string msg = "The connection has timed out while an operation was in flight. The default is 15000ms.";
                    operation.HandleClientError(msg, ResponseStatus.ClientFailure);
                    IsDead = true;
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
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
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var operation = (IOperation)asyncResult.AsyncState;
            try
            {
                var bytesRead = _networkStream.EndRead(asyncResult);
                if (bytesRead == 0)
                {
                    SendEvent.Set();
                    return;
                }
                operation.Read(operation.Buffer, 0, bytesRead);
                BufferManager.ReturnBuffer(operation.Buffer);

                if (operation.LengthReceived < operation.TotalLength)
                {
                    operation.Buffer = BufferManager.TakeBuffer(512);
                    _networkStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
                }
                else
                {
                    SendEvent.Set();
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
        }

        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public override void Dispose()
        {
            Log.Debug(m => m("Disposing connection for {0} - {1}", ConnectionPool.EndPoint, _identity));
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!Disposed)
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
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
                    }
                }
            }
            else
            {
                if (!Disposed)
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
            }
            Disposed = true;
        }

        ~EapConnection()
        {
            Log.Debug(m=>m("Finalizing connection for {0}", ConnectionPool.EndPoint));
            Dispose(false);
        }
    }
}