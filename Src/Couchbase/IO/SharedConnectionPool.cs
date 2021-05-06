using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;
using Couchbase.Logging;

namespace Couchbase.IO
{
    public class SharedConnectionPool<T> : ConnectionPoolBase<T> where T : class, IConnection
    {
        private static readonly ILog Log = LogManager.GetLogger<SharedConnectionPool<T>>();
        private readonly List<IConnection> _connections;
        private volatile int _currentIndex;
        private readonly Guid _identity = Guid.NewGuid();
        private readonly object _connectionsLock = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedConnectionPool{T}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="endPoint">The remote endpoint or server node to connect to.</param>
        public SharedConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint)
            : this(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new DefaultConverter())
        {
        }

        /// <summary>
        /// CTOR for testing/dependency injection.
        /// </summary>
        /// <param name="configuration">The <see cref="PoolConfiguration"/> to use.</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the Couchbase Server.</param>
        /// <param name="factory">A functory for creating <see cref="IConnection"/> objects./></param>
        /// <param name="converter">The <see cref="IByteConverter"/>that this instance is using.</param>
        internal SharedConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint,
            Func<IConnectionPool<T>, IByteConverter, BufferAllocator, T> factory, IByteConverter converter)
            : base(configuration, endPoint, factory, converter)
        {
            // default List<T> capacity is 4.  Let's guestimate a better one to avoid unnecessary resize allocations.
            var initialPoolCapacity = Math.Max(configuration.MinSize * 2, 10);
            initialPoolCapacity = Math.Min(configuration.MaxSize, initialPoolCapacity);
            _connections = new List<IConnection>(initialPoolCapacity);
        }

        public override IEnumerable<IConnection> Connections
        {
            get
            {
                IEnumerable<IConnection> result = null;
                lock (_connectionsLock)
                {
                    result = _connections.ToArray();
                }

                return result;
            }
            set { throw new NotSupportedException(); }
        }

        internal int GetIndex()
        {
            //we don't care necessarily about thread safety as long as
            //index is within the allowed range based off the size of the pool.
            // do not lock here, as it is called in sections that are already locked, and would deadlock.
            var index = _currentIndex % _connections.Count;
            if (_currentIndex > _connections.Count)
            {
                _currentIndex = 0;
            }
            else
            {
                _currentIndex++;
            }
            return index;
        }

        public override IConnection Acquire()
        {
            // ReSharper disable once InconsistentlySynchronizedField
            if (_connections.Count >= Configuration.MaxSize)
            {
                IConnection connection;
                lock (_connectionsLock)
                {
                    connection = _connections.ElementAtOrDefault(GetIndex());
                }

                try
                {
                    if (connection == null)
                    {
                        return Acquire();
                    }
                    Authenticate(connection);
                    EnableEnhancedAuthentication(connection);
                    return connection;
                }
                catch (Exception e)
                {
                    Log.Debug($"Connection creation or authentication failed for {connection?.ConnectionId}", e);
                    if (connection != null)
                    {
                        connection.IsDead = true;
                        Release((T) connection);
                    }

                    throw;
                }
            }
            lock (_connectionsLock)
            {
                var connection = CreateAndAuthConnection();
                _connections.Add(connection);
                return _connections?.ElementAtOrDefault(GetIndex());
            }
        }

        private IConnection CreateAndAuthConnection()
        {
            Log.Debug("Trying to acquire new connection! Refs={0}", _connections.Count);

            var connection = Factory(this, Converter, BufferAllocator);
            try
            {
                //Perform sasl auth
                Authenticate(connection);
                EnableEnhancedAuthentication(connection);

                Log.Debug("Acquire new: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5}",
                    connection.Identity, EndPoint, _connections.Count, Configuration.MaxSize, _identity, _disposed);

                return connection;
            }
            catch (Exception)
            {
                Log.Debug("Connection creation or authentication failed for {0}", connection?.ConnectionId);
                connection?.Dispose();
                throw;
            }
        }

        public override void Release(T connection)
        {
            if (connection == null) return;

            if (connection.IsDead)
            {
                lock (_connectionsLock)
                {
                    connection.Dispose();
                    Owner?.CheckOnline(connection.IsDead);
                    _connections.Remove(connection);
                }
            }
        }

        public override void Initialize()
        {
            try
            {
                lock (_connectionsLock)
                {
                    var connectionsToCreate = Configuration.MaxSize - _connections.Count;
                    for (var i = 0; i < connectionsToCreate; i++)
                    {
                        var connection = CreateAndAuthConnection();
                        _connections.Add(connection);
                    }
                    InitializationFailed = false;
                    //auth the connection used to select the SASL type to use for auth
                    foreach (var connection in _connections.Where(x=>!x.IsAuthenticated))
                    {
                        try
                        {
                            Authenticate(connection);
                            EnableEnhancedAuthentication(connection);
                        }
                        catch (Exception e)
                        {
                            InitializationFailed = true;
                            Log.Debug($"Connection creation or authentication failed for {connection?.ConnectionId}", e);
                            connection?.Dispose();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error("Initialize failed for " + _identity, e);
                InitializationFailed = true;
            }
        }

        public override void Dispose()
        {
            lock (_connectionsLock)
            {
                if (_disposed) return;
                _disposed = true;
                _connections.ForEach(x =>
                {
                    x.Dispose();
                    Owner?.CheckOnline(x.IsDead);
                });
                _connections.Clear();
            }
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
