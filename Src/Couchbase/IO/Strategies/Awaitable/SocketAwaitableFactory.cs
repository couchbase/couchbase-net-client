using System;
using System.Net.Sockets;
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