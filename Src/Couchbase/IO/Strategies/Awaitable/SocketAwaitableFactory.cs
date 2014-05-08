using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Configuration.Client;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// A functory for creating <see cref="SocketAwaitable"/> instances.
    /// </summary>
    internal static class SocketAwaitableFactory
    {
        /// <summary>
        /// Creates and returns a <see cref="SocketAwaitable"/> object based off of the <see cref="PoolConfiguration"/>.
        /// </summary>
        /// <returns></returns>
        public static Func<IConnectionPool, BufferAllocator, SocketAwaitable> GetSocketAwaitable()
        {
            Func<IConnectionPool, BufferAllocator, SocketAwaitable> factory = (p, b) =>
            {
                var connection = p.Acquire();
                var eventArgs = new SocketAsyncEventArgs
                {
                    AcceptSocket = connection.Socket,
                    UserToken = new OperationAsyncState
                    {
                        Connection = connection
                    }
                };
                b.SetBuffer(eventArgs);
                return new SocketAwaitable(eventArgs);
            };
            return factory;
        }
    }
}
