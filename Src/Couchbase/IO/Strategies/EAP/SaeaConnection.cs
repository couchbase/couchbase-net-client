using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.IO.Operations;
using Couchbase.IO.Strategies.Awaitable;
using Couchbase.IO.Utils;

namespace Couchbase.IO.Strategies.EAP
{
    internal class SaeaConnection : IConnection
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ConnectionPool<SaeaConnection> _connectionPool;
        private readonly Guid _identity = Guid.NewGuid();
        private readonly Socket _socket;
        private readonly AutoResetEvent WaitEvent = new AutoResetEvent(true);
        private readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        private readonly SocketAsyncEventArgs _socketAsync;
        private volatile bool _disposed;

        internal SaeaConnection(ConnectionPool<SaeaConnection> connectionPool, Socket socket) 
            : this(connectionPool, socket, new SocketAsyncEventArgs())
        {
            _connectionPool = connectionPool;
            _socket = socket;
        }

        internal SaeaConnection(ConnectionPool<SaeaConnection> connectionPool, Socket socket, SocketAsyncEventArgs socketAsync)
        {
            _connectionPool = connectionPool;
            _socket = socket;
            _socketAsync = socketAsync;
            _socketAsync.AcceptSocket = _socket;
            _socketAsync.Completed += SocketAsyncCompleted;
            State = new OperationAsyncState();
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

        public OperationAsyncState State { get; set; }

        public Socket Socket
        {
            get { return _socket; }
        }

        public Guid Identity
        {
            get { return _identity; }
        }

        public bool IsAuthenticated { get; set; }

        public void Send(byte[] buffer, int offset, int length, OperationAsyncState state)
        {
            State.Reset();
            WaitEvent.WaitOne();
            _socketAsync.UserToken = State;
            _socketAsync.SetBuffer(buffer, offset, length);
            _socket.SendAsync(_socketAsync);
            WaitEvent.Reset();
            SendEvent.WaitOne();
            WaitEvent.Set();
        }

        public void Receive(byte[] buffer, int offset, int length, OperationAsyncState state)
        {
            //Not needed
        }

        private void Send(SocketAsyncEventArgs e)
        {
            Log.Debug(m => m("send..."));
            if (e.SocketError == SocketError.Success)
            {
                var willRaiseCompletedEvent = _socket.ReceiveAsync(e);
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
                        SendEvent.Set();
                    }
                }
                else
                {
                    throw new SocketException((int)e.SocketError);
                }
                break;
            }
        }

        private static void CreateHeader(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            if (buffer.Length > 0)
            {
                state.Header = new OperationHeader
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

        private static void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            state.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, 28, state.Header.BodyLength),
            };
        }


        /// <summary>
        /// Shuts down, closes and disposes of the internal <see cref="Socket"/> instance.
        /// </summary>
        public void Dispose()
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
                    if (_socket != null)
                    {
                        if (_socket.Connected)
                        {
                            _socket.Shutdown(SocketShutdown.Both);
                            _socket.Close(_connectionPool.Configuration.ShutdownTimeout);
                        }
                        else
                        {
                            _socket.Close();
                            _socket.Dispose();
                        }
                    }
                }
            }
            else
            {
                if (_socket != null)
                {
                    _socket.Close();
                    _socket.Dispose();
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
