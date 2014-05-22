using System;
using System.Net.Security;
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


        public void Send(byte[] buffer, int offset, int length, Strategies.Awaitable.OperationAsyncState state)
        {
            throw new NotImplementedException();
        }

        public void Receive(byte[] buffer, int offset, int length, Strategies.Awaitable.OperationAsyncState state)
        {
            throw new NotImplementedException();
        }


        public Strategies.Awaitable.OperationAsyncState State
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }


        public Operations.IOperationResult<T> Send<T>(Operations.IOperation<T> operation)
        {
            throw new NotImplementedException();
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/

#endregion
