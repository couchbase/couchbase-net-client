using System;
using System.Net.Sockets;
using System.Threading;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies
{
    internal class SaeaConnection : ConnectionBase
    {
        private readonly ConnectionPool<SaeaConnection> _connectionPool;
        private readonly AutoResetEvent _sendEvent = new AutoResetEvent(false);
        private readonly SocketAsyncEventArgs _socketAsync;
        private volatile bool _disposed;

        internal SaeaConnection(ConnectionPool<SaeaConnection> connectionPool, Socket socket, IByteConverter converter) 
            : this(connectionPool, socket, new SocketAsyncEventArgs(), converter)
        {
        }

        internal SaeaConnection(ConnectionPool<SaeaConnection> connectionPool, Socket socket, SocketAsyncEventArgs socketAsync, IByteConverter converter) 
            : base(socket, converter)
        {
            _connectionPool = connectionPool;
            _socketAsync = socketAsync;
            _socketAsync.AcceptSocket = Socket;
            _socketAsync.Completed += SocketAsyncCompleted;
        }

        void SocketAsyncCompleted(object sender, SocketAsyncEventArgs e)
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

        public override IOperationResult<T> Send<T>(IOperation<T> operation)
        {
            State.Reset();
            _socketAsync.UserToken = State;

            var buffer = operation.GetBuffer();
            _socketAsync.SetBuffer(buffer, 0, buffer.Length);
            _socketAsync.AcceptSocket.SendAsync(_socketAsync);
            _sendEvent.WaitOne();

            operation.Header = State.Header;
            operation.Body = State.Body;
            return operation.GetResult();
        }

        private void Send(SocketAsyncEventArgs e)
        {
            Log.Debug(m => m("send..."));
            if (e.SocketError == SocketError.Success)
            {
                var willRaiseCompletedEvent = e.AcceptSocket.ReceiveAsync(e);
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

        private void Receive(SocketAsyncEventArgs e)
        {
            while (true)
            {
                if (e.SocketError == SocketError.Success)
                {
                    var state = (OperationAsyncState)e.UserToken;
                    state.BytesReceived += e.BytesTransferred;
                    state.Data.Write(e.Buffer, e.Offset, e.Count);
                    Log.Debug(m => m("receive...{0} bytes of {1} offset {2}", state.BytesReceived, e.Count, e.Offset));

                    if (state.Header.BodyLength == 0)
                    {
                        CreateHeader(state);
                        Log.Debug(m => m("received key {0}", state.Header.Key));
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
                        Log.Debug(m => m("bytes rcvd/length: {0}/{1}", state.BytesReceived, state.Header.TotalLength));
                        CreateBody(state);
                        _sendEvent.Set();
                    }
                }
                else
                {
                    throw new SocketException((int)e.SocketError);
                }
                break;
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
                }
            }
            else
            {
                if (Socket != null)
                {
                    Socket.Close();
                    Socket.Dispose();
                }
   
            }
            _disposed = true;
        }

        ~SaeaConnection()
        {
            Dispose(false);
        }
    }
}
