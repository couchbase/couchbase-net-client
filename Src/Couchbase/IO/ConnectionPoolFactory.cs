using System;
using System.Net;
using Couchbase.Configuration.Client;

#if NET45
using Couchbase.Configuration.Client.Providers;
#endif

namespace Couchbase.IO
{
    /// <summary>
    /// A factory creator for <see cref="IConnectionPool"/> instances.
    /// </summary>
    public class ConnectionPoolFactory
    {
        /// <summary>
        /// Gets the factory.
        /// </summary>
        /// <returns></returns>
        public static Func<PoolConfiguration, IPEndPoint, IConnectionPool> GetFactory()
        {
            return (config, endpoint) =>
            {
                IConnectionPool connectionPool;
                if (config.UseSsl)
                {
                    connectionPool = new ConnectionPool<SslConnection>(config, endpoint);
                }
                else
                {
                    if (config.ClientConfiguration.UseConnectionPooling)
                    {
                        connectionPool = new ConnectionPool<Connection>(config, endpoint);
                    }
                    else
                    {
                        connectionPool = new ConnectionPool<MultiplexingConnection>(config, endpoint);
                    }
                }
                return connectionPool;
            };
        }

#if NET45

        /// <summary>
        /// Gets the factory.
        /// </summary>
        /// <returns></returns>
        public static Func<PoolConfiguration, IPEndPoint, IConnectionPool> GetFactory(ConnectionPoolElement element)
        {
            return GetFactory(element.Type);
        }

#endif

        /// <summary>
        /// Gets the factory.
        /// </summary>
        /// <returns></returns>
        public static Func<PoolConfiguration, IPEndPoint, IConnectionPool> GetFactory(string typeName)
        {
            return (config, endpoint) =>
            {
                var type = Type.GetType(typeName);
                if (type == null)
                {
                    throw new TypeLoadException(string.Format("Could not find: {0}", typeName));
                }
                return (IConnectionPool) Activator.CreateInstance(type, config, endpoint);
            };
        }

        /// <summary>
        /// Gets the factory.
        /// </summary>
        /// <returns></returns>
        public static Func<PoolConfiguration, IPEndPoint, IConnectionPool> GetFactory<T>()
        {
            return (config, endpoint) =>
            {
                var type = typeof (T);
                return (IConnectionPool)Activator.CreateInstance(type, config, endpoint);
            };
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
