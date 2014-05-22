using System;
using System.Net.Security;
using System.Net.Sockets;
using Couchbase.Configuration.Client;
using Couchbase.IO.Strategies;

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
                if (config.EncryptTraffic)
                {
                    var ns = new NetworkStream(socket);
                    var ssls = new SslStream(ns);
                    ssls.AuthenticateAsClient(p.EndPoint.Address.ToString());
                }
                return new DefaultConnection(p, socket);
            };
            return factory;
        }

        /// <summary>
        /// Returns a functory for creating <see cref="DefaultConnection"/> objects.
        /// </summary>
        /// <returns>A <see cref="DefaultConnection"/> based off of the <see cref="PoolConfiguration"/> of the <see cref="IConnectionPool"/>.</returns>
        internal static Func<ConnectionPool<T>, T> GetGeneric<T>() where T : class, IConnection
        {
            Func<IConnectionPool<T>, T> factory = p => 
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

                //TODO refactor
                IConnection connection = null;
                if (p.Configuration.EncryptTraffic)
                {
                    var pool = p as ConnectionPool<SslConnection>;
                    connection = new SslConnection(pool, socket);
                    ((SslConnection)connection).Authenticate();
                }
                else
                {
                    //TODO this should be from T...
                    var pool = p as ConnectionPool<EapConnection>;
                    connection = new EapConnection(pool, socket);
                }
                return connection as T;
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
