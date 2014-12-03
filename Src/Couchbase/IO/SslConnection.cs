using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using Couchbase.Core.Diagnostics;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;

namespace Couchbase.IO
{
    internal class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private volatile bool _timingEnabled;

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
            _timingEnabled = Configuration.EnableOperationTiming;
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

        public override void Send<T>(IOperation<T> operation)
        {
            try
            {
                _sslStream.BeginWrite(operation.WriteBuffer, 0, operation.WriteBuffer.Length, SendCallback, operation);
                if (!SendEvent.WaitOne(Configuration.ConnectionTimeout))
                {
                    const string msg =
                        "The connection has timed out while an operation was in flight. The default is 15000ms.";
                    operation.HandleClientError(msg, ResponseStatus.ClientFailure);
                    IsDead = true;
                }
            }
            catch (Exception e)
            {
                HandleException(e, operation);
            }
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
#if DEBUG
        ~SslConnection()
        {
            Dispose(false);
        }
#endif
    }
}
