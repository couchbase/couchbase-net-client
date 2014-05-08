using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies.Async
{
    /// <summary>
    /// A factory class for creating <see cref="SocketAsyncEventArgs"/> objects.
    /// </summary>
    internal static class SocketAsyncFactory
    {
        /// <summary>
        /// Gets a functory for creating <see cref="SocketAsyncEventArgs"/>
        /// </summary>
        /// <returns></returns>
        public static Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> GetSocketAsyncFunc()
        {
            Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> factory = (p, b) =>
            {
                var connection = p.Acquire();
                var socketAsyncEventArgs = new SocketAsyncEventArgs
                {
                    AcceptSocket = connection.Socket,
                    UserToken = new OperationAsyncState
                    {
                        Connection = connection
                    }
                };
                b.SetBuffer(socketAsyncEventArgs);
                return socketAsyncEventArgs;
            };
            return factory;
        }
    }
}
