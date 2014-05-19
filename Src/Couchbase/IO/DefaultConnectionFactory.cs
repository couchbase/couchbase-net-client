using System;
using System.Net.Sockets;
using Couchbase.Configuration.Client;

namespace Couchbase.IO
{
    /// <summary>
    /// A factory creator for <see cref="IConnection"/>s
    /// </summary>
    public static class DefaultConnectionFactory
    {
        /// <summary>
        /// Returns a functory for creating <see cref="DefaultConnection"/> objects.
        /// </summary>
        /// <returns>A <see cref="DefaultConnection"/> based off of the <see cref="PoolConfiguration"/> of the <see cref="IConnectionPool"/>.</returns>
        internal static Func<IConnectionPool, IConnection> GetDefault()
        {
            Func<IConnectionPool, IConnection> factory = p =>
            {
                var config = p.Configuration;
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    ReceiveTimeout = config.RecieveTimeout,
                    SendTimeout = config.SendTimeout,
                };
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, config.RecieveTimeout);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, config.SendTimeout);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.Connect(p.EndPoint);
                return new DefaultConnection(p, socket);
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