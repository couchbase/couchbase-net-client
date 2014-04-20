using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies.Async
{
    internal static class SocketAsyncFactory
    {
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
