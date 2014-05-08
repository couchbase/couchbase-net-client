using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a TCP connection to a CouchbaseServer.
    /// </summary>
    internal sealed class DefaultConnection : IConnection
    {
        private readonly IConnectionPool _connectionPool;
        private readonly Socket _socket;
        private readonly Guid _identity = Guid.NewGuid();
        private bool _disposed;

        internal DefaultConnection(IConnectionPool connectionPool, Socket socket)
        {
            _connectionPool = connectionPool;
            _socket = socket;
        }

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

        ~DefaultConnection()
        {
            Dispose(false);
        }
    }
}
