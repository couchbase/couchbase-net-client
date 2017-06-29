using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core.Transcoders;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations.Authentication;
using Couchbase.Logging;

namespace Couchbase.IO
{
    public class SharedConnectionPool<T> : ConnectionPoolBase<T> where T : class, IConnection
    {
        private static readonly ILog Log = LogManager.GetLogger<SharedConnectionPool<T>>();
        readonly List<IConnection> _connections = new List<IConnection>();
        private volatile int _currentIndex;
        private readonly Guid _identity = Guid.NewGuid();
        private readonly object _lockObj = new object();
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SharedConnectionPool{T}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="endPoint">The remote endpoint or server node to connect to.</param>
        public SharedConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint)
            : base(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new DefaultConverter())
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
        }

        public override IEnumerable<IConnection> Connections
        {
            get { return _connections; }
            set { throw new NotSupportedException(); }
        }

        internal int GetIndex()
        {
            //we don't care necessarily about thread safety as long as
            //index is within the allowed range based off the size of the pool.
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
                var connection = _connections[GetIndex()];
                EnableEnhancedAuthentication(connection);
                return connection;
            }
            lock (_lockObj)
            {
                var connection = CreateAndAuthConnection();
                _connections.Add(connection);
                return _connections[GetIndex()];
            }
        }

        private IConnection CreateAndAuthConnection()
        {
            Log.Debug("Trying to acquire new connection! Refs={0}", _connections.Count);

            var connection = Factory(this, Converter, BufferAllocator);

            //Perform sasl auth
            Authenticate(connection);
            EnableEnhancedAuthentication(connection);

            Log.Debug("Acquire new: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5}",
                    connection.Identity, EndPoint, _connections.Count, Configuration.MaxSize, _identity, _disposed);

            return connection;
        }

        public override void Release(T connection)
        {
            if (connection == null) return;
            if (!connection.IsAuthenticated)
            {
                Authenticate(connection);
                EnableEnhancedAuthentication(connection);
            }
            connection.MarkUsed(false);
            if (connection.IsDead)
            {
                lock (_lockObj)
                {
                    connection.Dispose();
                    if (Owner != null)
                    {
                        Owner.CheckOnline(connection.IsDead);
                    }
                    _connections.Remove(connection);
                }
            }
        }

        public override void Initialize()
        {
            try
            {
                lock (_lockObj)
                {
                    var connectionsToCreate = Configuration.MaxSize - _connections.Count;
                    for (var i = 0; i < connectionsToCreate; i++)
                    {
                        var connection = CreateAndAuthConnection();
                        _connections.Add(connection);
                    }
                    //auth the connection used to select the SASL type to use for auth
                    foreach (var connection in _connections.Where(x=>!x.IsAuthenticated))
                    {
                        Authenticate(connection);
                        EnableEnhancedAuthentication(connection);
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
            lock (_lockObj)
            {
                if (_disposed) return;
                _disposed = true;
                _connections.ForEach(x =>
                {
                    x.Dispose();
                    if (Owner != null)
                    {
                        Owner.CheckOnline(x.IsDead);
                    }
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
