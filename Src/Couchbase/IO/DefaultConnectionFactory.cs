using System;
using System.Net.Security;
using System.Net.Sockets;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.IO.Utils;

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
        internal static Func<ConnectionPool<T>, IByteConverter, BufferAllocator, T> GetGeneric<T>() where T : class, IConnection
        {
            Func<IConnectionPool<T>, IByteConverter, BufferAllocator, T> factory = (p, c, b) =>
            {
                var socket = new Socket(p.EndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
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

                IConnection connection;
                if (p.Configuration.UseSsl)
                {
                    connection = new SslConnection(p, socket, c, b);
                    connection.Authenticate();
                }
                else
                {
                    connection = Activator.CreateInstance(typeof(T), p, socket, c, b) as T;
                }
                //need to be able to completely disable the feature if false - this should work
                if (p.Configuration.EnableTcpKeepAlives)
                {
                    socket.SetKeepAlives(p.Configuration.EnableTcpKeepAlives,
                        p.Configuration.TcpKeepAliveTime,
                        p.Configuration.TcpKeepAliveInterval);
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
