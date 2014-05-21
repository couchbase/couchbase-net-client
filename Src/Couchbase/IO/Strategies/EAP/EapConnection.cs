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
    internal class EapConnection : IConnection
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ConnectionPool<EapConnection> _connectionPool;
        private readonly NetworkStream _networkStream ;
        private readonly Socket _socket;
        private readonly Guid _identity = Guid.NewGuid();
        private readonly AutoResetEvent SendEvent = new AutoResetEvent(false);
        private readonly AutoResetEvent ReceiveEvent = new AutoResetEvent(false);
        private volatile bool _disposed;

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket) 
            : this(connectionPool, socket, new NetworkStream(socket))
        {
            _connectionPool = connectionPool;
            _socket = socket;
        }

        internal EapConnection(ConnectionPool<EapConnection> connectionPool, Socket socket, NetworkStream networkStream)
        {
            _connectionPool = connectionPool;
            _socket = socket;
            _networkStream = networkStream;
            State = new OperationAsyncState();
        }

        public void Authenticate()
        {
        }

        public void Send(byte[] buffer, int offset, int length, OperationAsyncState state)
        {
            State.Reset();
            _networkStream.BeginWrite(buffer, offset, length, SendCallback, State);
            SendEvent.WaitOne();
        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            _networkStream.EndWrite(asyncResult);
            SendEvent.Set();
        }

        public void Receive(byte[] buffer, int offset, int length, OperationAsyncState state)
        {
            _networkStream.BeginRead(buffer, offset, length, ReceiveCallback, State);
            ReceiveEvent.WaitOne();
        }

        private void ReceiveCallback(IAsyncResult asyncResult)
        {
            var state = asyncResult.AsyncState as OperationAsyncState;
            
            var bytesRead = _networkStream.EndRead(asyncResult);
            state.BytesReceived += bytesRead;
            state.Data.Write(state.Buffer, 0, bytesRead);

            Log.Debug(m => m("Bytes read {0}", state.BytesReceived));

            if (state.Header.BodyLength == 0)
            {
                CreateHeader(state);
                Log.Debug(m => m("received key {0}", state.Header.Key));
            }
            if (state.BytesReceived > 0 && state.BytesReceived < state.Header.TotalLength)
            {
                _networkStream.BeginRead(state.Buffer, 0, state.Buffer.Length, ReceiveCallback, state);
            }
            else
            {
                CreateBody(state);
                ReceiveEvent.Set();
            }
        }

        private void CreateHeader(OperationAsyncState state)
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

        private void CreateBody(OperationAsyncState state)
        {
            var buffer = state.Data.GetBuffer();
            state.Body = new OperationBody
            {
                Extras = new ArraySegment<byte>(buffer, OperationBase<object>.HeaderLength, state.Header.ExtrasLength),
                Data = new ArraySegment<byte>(buffer, 28, state.Header.BodyLength),
            };
        }

        public OperationAsyncState State { get; set; }

        /// <summary>
        /// True if the connection has been SASL authenticated.
        /// </summary>
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// Unique identifier for this connection.
        /// </summary>
        public Guid Identity
        {
            get { return _identity; }
        }

        /// <summary>
        /// The Socket used for IO.
        /// </summary>
        public Socket Socket
        {
            get { return _socket; }
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
                    if (_networkStream != null)
                    {
                        _networkStream.Dispose();
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
