using System;
using System.Net.Sockets;

namespace Couchbase.IO
{
    internal class DefaultConnection : IConnection
    {
        private readonly IConnectionPool _connectionPool;
        private readonly Socket _handle;
        private readonly Guid _identity = Guid.NewGuid();
        private bool _disposed;

        internal DefaultConnection(IConnectionPool connectionPool, Socket handle)
        {
            _connectionPool = connectionPool;
            _handle = handle;
        }

        public Guid Identity
        {
            get { return _identity; }
        }

        public Socket Handle
        {
            get { return _handle; }
        }

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
                    if (_handle != null)
                    {
                        if (_handle.Connected)
                        {
                            _handle.Shutdown(SocketShutdown.Both);
                            _handle.Close(_connectionPool.Configuration.ShutdownTimeout);
                        }
                        else
                        {
                            _handle.Close();
                            _handle.Dispose();
                        }
                    }
                }
            }
            else
            {
                if (_handle != null)
                {
                    _handle.Close();
                    _handle.Dispose();
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
