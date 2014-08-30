using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies
{
    internal class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, IByteConverter converter) 
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket)), converter)
        {
        }

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, SslStream sslStream, IByteConverter converter) 
            : base(socket, converter)
        {
            ConnectionPool = connectionPool;
            _sslStream = sslStream;
            Configuration = ConnectionPool.Configuration;
        }

        public void Authenticate()
        {
            try
            {
                var targetHost = ConnectionPool.EndPoint.Address.ToString();
                Log.Warn(m => m("Starting SSL encryption on {0}", targetHost));
                _sslStream.AuthenticateAsClient(targetHost);
                IsSecure = true;
            }
            catch (AuthenticationException e)
            {
                Log.Error(e);
            }
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            try
            {
                operation.Reset();
                var buffer = operation.Write();

                _sslStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, operation);
                if (!SendEvent.WaitOne(Configuration.OperationTimeout))
                {
                    const string msg = "Operation timed out: the timeout can be configured by changing the PoolConfiguration.OperationTimeout property. The default is 2500ms.";
                    operation.HandleClientError(msg);
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
                _sslStream.EndWrite(asyncResult);
                operation.Buffer = BufferManager.TakeBuffer(512);
                _sslStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
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
                var bytesRead = _sslStream.EndRead(asyncResult);
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
                    _sslStream.BeginRead(operation.Buffer, 0, operation.Buffer.Length, ReceiveCallback, operation);
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
                    if (_sslStream != null)
                    {
                        _sslStream.Dispose();
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
                    if (_sslStream != null)
                    {
                        _sslStream.Dispose();
                    }
                }
            }
            Disposed = true;
        }

        ~SslConnection()
        {
            Dispose(false);
        }
    }
}
