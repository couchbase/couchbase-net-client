using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Logging;
using Couchbase.Configuration.Client;
using Couchbase.IO.Converters;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a pool of TCP connections to a Couchbase Server node.
    /// </summary>
    public class ConnectionPool<T> : ConnectionPoolBase<T> where T : class, IConnection
    {
        // ReSharper disable once StaticMemberInGenericType
        private static readonly ILog Log = LogManager.GetLogger<ConnectionPool<IConnection>>();
        private readonly ConcurrentQueue<T> _store = new ConcurrentQueue<T>();
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly object _lock = new object();
        private int _count;
        private bool _disposed;
        private readonly ConcurrentDictionary<Guid, T> _refs = new ConcurrentDictionary<Guid, T>();
        private int _acquireFailedCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPool{T}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="endPoint">The remote endpoint or server node to connect to.</param>
        public ConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint)
            : base(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new DefaultConverter())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPool{T}"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="endPoint">The end point.</param>
        /// <param name="factory">The factory.</param>
        /// <param name="converter">The converter.</param>
        internal ConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint,
           Func<IConnectionPool<T>, IByteConverter, BufferAllocator, T> factory, IByteConverter converter)
            : base(configuration, endPoint, factory, converter)
        {
        }

        /// <summary>
        /// Returns a collection of <see cref="IConnection"/> objects.
        /// </summary>
        /// <remarks>Only returns what is available in the queue at the point in time it is called.</remarks>
        public override IEnumerable<IConnection> Connections
        {
            get { return _store.ToArray(); }
        }

        /// <summary>
        /// Gets the number of <see cref="IConnection"/> within the pool, whether or not they are available or not.
        /// </summary>
        /// <returns></returns>
        public override int Count()
        {
            return _count;
        }

        /// <summary>
        /// Sets the initial state of the pool and adds the MinSize of <see cref="IConnection"/> object to the pool.
        /// </summary>After the <see cref="PoolConfiguration.MinSize"/> is reached, the pool will grow to <see cref="PoolConfiguration.MaxSize"/>
        /// and any pending requests will then wait for a <see cref="IConnection"/> to be released back into the pool.
        /// <remarks></remarks>
        public override void Initialize()
        {
            lock (_lock)
            {
                // make sure all existing connections are authenticated and enable
                // enhanved auth when required
                foreach (var connection in _refs.Values)
                {
                    Authenticate(connection);
                    EnableEnhancedAuthentication(connection);
                }

                // create and configure connections to minsize
                while (_refs.Count < Configuration.MinSize)
                {
                    try
                    {
                        var connection = Factory(this, Converter, BufferAllocator);

                        Authenticate(connection);
                        EnableEnhancedAuthentication(connection);

                        Log.Info("Initializing connection on [{0} | {1}] - {2} - Disposed: {3}",
                            EndPoint, connection.Identity, Identity, _disposed);

                        _store.Enqueue(connection);
                        _refs.TryAdd(connection.Identity, connection);
                        Interlocked.Increment(ref _count);
                    }
                    catch (Exception e)
                    {
                        Log.Info("Node {0} failed to initialize, reason: {1}", EndPoint, e);
                        InitializationFailed = true;
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a <see cref="IConnection"/> the pool, creating a new one if none are available
        /// and the <see cref="PoolConfiguration.MaxSize"/> has not been reached.
        /// </summary>
        /// <returns>A TCP <see cref="IConnection"/> object to a Couchbase Server.</returns>
        /// <exception cref="ConnectionUnavailableException">thrown if a thread waits more than the <see cref="PoolConfiguration.MaxAcquireIterationCount"/>.</exception>
        public override IConnection Acquire()
        {
            T connection = AcquireFromPool();
            if (connection != null)
            {
                Authenticate(connection);
                EnableEnhancedAuthentication(connection);
                return connection;
            }

            lock (_lock)
            {
                //try to get connection from pool
                //in case connection released while operation waited in Monitor.Enter (lock)
                connection = AcquireFromPool();
                if (connection != null)
                {
                    Authenticate(connection);
                    EnableEnhancedAuthentication(connection);
                    return connection;
                }

                if (_count < Configuration.MaxSize && !_disposed)
                {
                    Log.Debug("Trying to acquire new connection! Refs={0}", _refs.Count);
                    connection = Factory(this, Converter, BufferAllocator);

                    Authenticate(connection);
                    EnableEnhancedAuthentication(connection);

                    _refs.TryAdd(connection.Identity, connection);

                    Log.Debug("Acquire new: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5}",
                        connection.Identity, EndPoint, _store.Count, _count, Identity, _disposed);

                    Interlocked.Increment(ref _count);
                    Interlocked.Exchange(ref _acquireFailedCount, 0);
                    connection.MarkUsed(true);
                    return connection;
                }
            }

            _autoResetEvent.WaitOne(Configuration.WaitTimeout);
            var acquireFailedCount = Interlocked.Increment(ref _acquireFailedCount);
            if (acquireFailedCount >= Configuration.MaxAcquireIterationCount)
            {
                Interlocked.Exchange(ref _acquireFailedCount, 0);
                const string msg = "Failed to acquire a pooled client connection on {0} after {1} tries.";
                throw new ConnectionUnavailableException(msg, EndPoint, acquireFailedCount);
            }
            return Acquire();
        }

        /// <summary>
        /// Returns a <see cref="IConnection"/> from the pool.
        /// </summary>
        /// <returns>A TCP <see cref="IConnection"/> object to a Couchbase Server.</returns>
        private T AcquireFromPool()
        {
            T connection;

            if (_store.TryDequeue(out connection) && !_disposed)
            {
                Interlocked.Exchange(ref _acquireFailedCount, 0);
                Log.Debug("Acquire existing: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5} - Refs={6}",
                    connection.Identity, EndPoint, _store.Count, _count, Identity, _disposed, _refs.Count);

                connection.MarkUsed(true);
                return connection;
            }

            return null;
        }

        /// <summary>
        /// Releases an acquired <see cref="IConnection"/> object back into the pool so that it can be reused by another operation.
        /// </summary>
        /// <param name="connection">The <see cref="IConnection"/> to release back into the pool.</param>
        public override void Release(T connection)
        {
            if (connection == null) return;
            Log.Debug("Releasing: {0} on {1} - {2} - Refs={3}", connection.Identity, EndPoint, Identity, _refs.Count);
            connection.MarkUsed(false);
            if (connection.IsDead)
            {
                connection.Dispose();
                Interlocked.Decrement(ref _count);
                Log.Debug("Connection is dead: {0} on {1} - {2} - [{3}, {4}] ",
                    connection.Identity, EndPoint, Identity, _store.Count, _count);

                if (Owner != null)
                {
                    Owner.CheckOnline(connection.IsDead);
                }

                lock (_lock)
                {
                    T old;
                    if (_refs.TryRemove(connection.Identity, out old))
                    {
                        old.Dispose();
                    }
                }
            }
            else
            {
                lock (_store)
                {
                    if (!_store.Contains(connection))
                    {
                        _store.Enqueue(connection);
                    }
                }
            }
            Log.Debug("Released: {0} on {1} - {2} - Refs={3}", connection.Identity, EndPoint, Identity, _refs.Count);
            _autoResetEvent.Set();
        }

        /// <summary>
        /// Removes and disposes all <see cref="IConnection"/> objects in the pool.
        /// </summary>
        public override void Dispose()
        {
            Log.Debug("Disposing ConnectionPool for {0} - {1}", EndPoint, Identity);
            lock (_lock)
            {
                Dispose(true);
            }
        }

        public void Dispose(bool disposing)
        {
            if (disposing)
            {
                GC.SuppressFinalize(this);
            }
            if (!_disposed)
            {
                _disposed = true;
                var interval = Configuration.CloseAttemptInterval;

                foreach (var key in _refs.Keys)
                {
                    Log.Debug("Trying to close conn {0}", key);
                    T conn;
                    if (_refs.TryGetValue(key, out conn) && conn != null && !conn.HasShutdown)
                    {
                        Log.Debug("Closing conn {0} - ", key, conn.Identity);
                        if (conn.InUse)
                        {
                            conn.CountdownToClose(interval);
                        }
                        else
                        {
                            lock (conn)
                            {
                                if (!conn.InUse)
                                {
                                    conn.Dispose();

                                    T storedConn;
                                    _refs.TryRemove(key, out storedConn);
                                }
                            }
                        }
                    }
                }
            }
        }

#if DEBUG
        ~ConnectionPool()
        {
            try
            {
                Log.Debug("Finalizing ConnectionPool for {0}", EndPoint);
                Dispose(false);
            }
            catch (Exception e)
            {
                //TODO temp fix since they may getting finalized...
                try
                {
                    Log.Debug(e);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                }
            }
        }
#endif
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
