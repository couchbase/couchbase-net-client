using System;
using System.Net.Security;
using System.Net.Sockets;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Strategies;

namespace Couchbase.IO
{
    /// <summary>
    /// A factory creator for <see cref="IConnection"/>s
    /// </summary>
    public static class DefaultConnectionFactory
    {
        /// <summary>
        /// Returns a functory for creating <see cref="Connection"/> objects.
        /// </summary>
        /// <returns>A <see cref="Connection"/> based off of the <see cref="PoolConfiguration"/> of the <see cref="IConnectionPool"/>.</returns>
        internal static Func<IConnectionPool, IConnection> GetDefault()
        {
            Func<IConnectionPool, IConnection> factory = p =>
            {
                var config = p.Configuration;
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                socket.Connect(p.EndPoint);
                if (config.UseSsl)
                {
                    var ns = new NetworkStream(socket);
                    var ssls = new SslStream(ns);
                    ssls.AuthenticateAsClient(p.EndPoint.Address.ToString());
                }
                return null;//new DefaultConnection(p, socket);
            };
            return factory;
        }

        /// <summary>
        /// Returns a functory for creating <see cref="Connection"/> objects.
        /// </summary>
        /// <returns>A <see cref="Connection"/> based off of the <see cref="PoolConfiguration"/> of the <see cref="IConnectionPool"/>.</returns>
        internal static Func<ConnectionPool<T>, IByteConverter, T> GetGeneric<T>() where T : class, IConnection
        {
            Func<IConnectionPool<T>, IByteConverter, T> factory = (p, c) =>
            {
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                var asyncResult = socket.BeginConnect(p.EndPoint, null, null);
                var waitHandle = asyncResult.AsyncWaitHandle;

                if (waitHandle.WaitOne(p.Configuration.ConnectTimeout, true)
                     && socket.Connected)
                {
                    socket.EndConnect(asyncResult);
                }
                else
                {
                    socket.Close();
                    const int connectionTimedOut = 10060;
                    throw new SocketException(connectionTimedOut);
                }

                //TODO refactor
                IConnection connection;
                if (p.Configuration.UseSsl)
                {
                    var pool = p as ConnectionPool<SslConnection>;
                    connection = new SslConnection(pool, socket, c);
                    ((SslConnection)connection).Authenticate();
                }
                else
                {
                    //TODO this should be from T...
                    var pool = p as ConnectionPool<Connection>;
                    connection = new Connection(pool, socket, c);
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
