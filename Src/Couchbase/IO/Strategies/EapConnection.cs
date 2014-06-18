using System;
using System.Net.Sockets;
using System.Threading;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies
{
    internal class EapConnection : ConnectionBase
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
            State.Reset();
            var buffer = operation.GetBuffer();
            State.Offset = operation.Offset;

            _networkStream.BeginWrite(buffer, 0, buffer.Length, SendCallback, State);
            _sendEvent.WaitOne();
            operation.Header = State.Header;
            operation.Body = State.Body;
            return operation.GetResult();
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            _networkStream.EndWrite(asyncResult);
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }
            _networkStream.BeginRead(state.Buffer, 0, State.Buffer.Length, ReceiveCallback, State);
        }


        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            if (state == null)
            {
                throw new NullReferenceException("state cannot be null.");
            }

            var bytesRead = _networkStream.EndRead(asyncResult);
            state.BytesReceived += bytesRead;
            state.Data.Write(state.Buffer, 0, bytesRead);

            Log.Debug(m => m("Bytes read {0} using {1} on thread {2}", state.BytesReceived, Identity, Thread.CurrentThread.ManagedThreadId));

            if (state.Header.BodyLength == 0)
            {
                CreateHeader(state);
            }
            if (state.BytesReceived > 0 && state.BytesReceived < state.Header.TotalLength)
            {
                _networkStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
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
