using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies
{
    internal class SslConnection : ConnectionBase
    {
        private readonly SslStream _sslStream;
        private readonly ConnectionPool<SslConnection> _connectionPool;
        private readonly AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private volatile bool _disposed;

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, IByteConverter converter) 
            : this(connectionPool, socket, new SslStream(new NetworkStream(socket)), converter)
        {
        }

        internal SslConnection(ConnectionPool<SslConnection> connectionPool, Socket socket, SslStream sslStream, IByteConverter converter) 
            : base(socket, converter)
        {
            _connectionPool = connectionPool;
            _sslStream = sslStream;
        }

        public void Authenticate()
        {
            var targetHost = _connectionPool.EndPoint.Address.ToString();
            Log.Warn(m => m("Starting SSL encryption on {0}", targetHost));
            _sslStream.AuthenticateAsClient(targetHost);
        }

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            State.Reset();
            var buffer = operation.GetBuffer();
            _sslStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, State);
            _sendEvent.WaitOne();
            operation.Header = State.Header;
            operation.Body = State.Body;
            return operation.GetResult();
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }
            _sslStream.EndWrite(asyncResult);
            _sslStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, State);
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }

            var bytesRead = _sslStream.EndRead(asyncResult);
            state.BytesReceived += bytesRead;
            state.Data.Write(state.Buffer, 0, bytesRead);

            Log.Debug(m => m("Bytes read {0} of {1}", state.BytesReceived, state.Header.TotalLength));

            if (state.Header.BodyLength == 0)
            {
                CreateHeader(state);
                Log.Debug(m => m("received key {0}", state.Header.Key));
            }
            if (state.BytesReceived > 0 && state.BytesReceived < state.Header.TotalLength)
            {
                _sslStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
            }
            else
            {
                CreateBody(state);
                _sendEvent.Set();
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
                    if (_sslStream != null)
                    {
                        _sslStream.Dispose();
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
                if (_sslStream != null)
                {
                    _sslStream.Dispose();
                }
            }
            _disposed = true;
        }

        ~SslConnection()
        {
            Dispose(false);
        }
    }
}
