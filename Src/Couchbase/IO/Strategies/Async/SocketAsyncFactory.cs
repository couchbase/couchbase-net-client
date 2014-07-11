using System;
using System.Net.Sockets;
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