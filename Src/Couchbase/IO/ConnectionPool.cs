using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Channels;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.IO.Converters;

namespace Couchbase.IO
{
    internal class ConnectionPool<T> : IConnectionPool<T> where T : class, IConnection
    {
        private static readonly ILog Log = LogManager.GetLogger<ConnectionPool<T>>();
        private readonly ConcurrentQueue<T> _store = new ConcurrentQueue<T>();
        private readonly Func<ConnectionPool<T>, IByteConverter, T> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly PoolConfiguration _configuration;
        private readonly object _lock = new object();
        private readonly IByteConverter _converter;
        private int _count;
        private bool _disposed;
        private ConcurrentBag<T> _refs = new ConcurrentBag<T>();
        private Guid _identity = Guid.NewGuid();
        private int _acquireFailedCount;
        private readonly IServer _owner;

        public ConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint)
            : this(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new DefaultConverter())
        {
        }

        /// <summary>
        /// CTOR for testing/dependency injection.
        /// </summary>
        /// <param name="configuration">The <see cref="PoolConfiguration"/> to use.</param>
        /// <param name="endPoint">The <see cref="IPEndPoint"/> of the Couchbase Server.</param>
        /// <param name="factory">A functory for creating <see cref="IConnection"/> objects./></param>
        public ConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint, Func<ConnectionPool<T>, IByteConverter, T> factory, IByteConverter converter)
        {
            _configuration = configuration;
            _factory = factory;
            _converter = converter;
            EndPoint = endPoint;
        }

        /// <summary>
        /// The configuration passed into the pool when it is created. It has fields
        /// for MaxSize, MinSize, etc.
        /// </summary>
        public PoolConfiguration Configuration
        {
            get { return _configuration; }
        }

        /// <summary>
        /// The <see cref="IPEndPoint"/> of the server that the <see cref="IConnection"/>s are connected to.
        /// </summary>
        public IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Returns a collection of <see cref="IConnection"/> objects.
        /// </summary>
        /// <remarks>Only returns what is available in the queue at the point in time it is called.</remarks>
        public IEnumerable<T> Connections
        {
            get { return _store.ToArray(); }
        }

        /// <summary>
        /// Gets the number of <see cref="IConnection"/> within the pool, whether or not they are available or not.
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            return _count;
        }

        /// <summary>
        /// Sets the initial state of the pool and adds the MinSize of <see cref="IConnection"/> object to the pool.
        /// </summary>After the <see cref="PoolConfiguration.MinSize"/> is reached, the pool will grow to <see cref="PoolConfiguration.MaxSize"/>
        /// and any pending requests will then wait for a <see cref="IConnection"/> to be released back into the pool.
        /// <remarks></remarks>
        public void Initialize()
        {
            lock (_lock)
            {
                _count = _configuration.MinSize;
                do
                {
                    var connection = _factory(this, _converter);
                    Log.Debug(m => m("Initializing connection on [{0} | {1}] - {2} - Disposed: {3}",
                        EndPoint, connection.Identity, _identity, _disposed));

                    _store.Enqueue(connection);
                    _refs.Add(connection);
                } while (_store.Count < _count);
            }
        }

        /// <summary>
        /// Returns a <see cref="IConnection"/> the pool, creating a new one if none are available
        /// and the <see cref="PoolConfiguration.MaxSize"/> has not been reached.
        /// </summary>
        /// <returns>A TCP <see cref="IConnection"/> object to a Couchbase Server.</returns>
        /// <exception cref="ConnectionUnavailableException">thrown if a thread waits more than the <see cref="PoolConfiguration.MaxAcquireIterationCount"/>.</exception>
        public T Acquire()
        {
            T connection;

            if (_store.TryDequeue(out connection) && !_disposed)
            {
                Interlocked.Exchange(ref _acquireFailedCount, 0);
                Log.Debug(m => m("Acquire existing: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5}",
                    connection.Identity, EndPoint, _store.Count, _count, _identity,_disposed));

                connection.MarkUsed(true);
                return connection;
            }

            lock (_lock)
            {
                if (_count < _configuration.MaxSize && !_disposed)
                {
                    Log.Debug("Trying to acquire new connection!");
                    connection = _factory(this, _converter);
                    _refs.Add(connection);

                    Log.Debug(m => m("Acquire new: {0} | {1} | [{2}, {3}] - {4} - Disposed: {5}",
                        connection.Identity, EndPoint, _store.Count, _count, _identity, _disposed));

                    Interlocked.Increment(ref _count);
                    Interlocked.Exchange(ref _acquireFailedCount, 0);
                    connection.MarkUsed(true);
                    return connection;
                }
            }

            _autoResetEvent.WaitOne(_configuration.WaitTimeout);
            var acquireFailedCount = Interlocked.Increment(ref _acquireFailedCount);
            if (acquireFailedCount >= _configuration.MaxAcquireIterationCount)
            {
                Interlocked.Exchange(ref _acquireFailedCount, 0);
                const string msg = "Failed to acquire a connection after {0} tries.";
                throw new ConnectionUnavailableException(msg, acquireFailedCount);
            }

            Log.Debug(m => m("No connections currently available on {0} - {1}. Trying again[{2}]. - Disposed: {3}",
                EndPoint, _identity, _acquireFailedCount, _disposed));

            return Acquire();
        }

        /// <summary>
        /// Releases an acquired <see cref="IConnection"/> object back into the pool so that it can be reused by another operation.
        /// </summary>
        /// <param name="connection">The <see cref="IConnection"/> to release back into the pool.</param>
        public void Release(T connection)
        {
            Log.Debug(m => m("Releasing: {0} on {1} - {2}", connection.Identity, EndPoint, _identity));
            connection.MarkUsed(false);
            if (connection.IsDead)
            {
                connection.Dispose();
                Interlocked.Decrement(ref _count);
                Log.Debug(m => m("Connection is dead: {0} on {1} - {2} - [{3}, {4}] ",
                    connection.Identity, EndPoint, _identity, _store.Count, _count));

                if (Owner != null)
                {
                    Owner.TakeOffline(connection.IsDead);
                }
            }
            else
            {
                _store.Enqueue(connection);
            }
            _autoResetEvent.Set();
        }

        /// <summary>
        /// Removes and disposes all <see cref="IConnection"/> objects in the pool.
        /// </summary>
        public void Dispose()
        {
            Log.Debug(m => m("Disposing ConnectionPool for {0} - {1}", EndPoint, _identity));
            Dispose(true);
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
                var interval = _configuration.CloseAttemptInterval;

                T conn;
                while (_refs.Count > 0)
                {
                    if (_refs.TryTake(out conn) && !conn.HasShutdown)
                    {
                        if (conn.InUse)
                        {
                            conn.CountdownToClose(interval);
                            _refs.Add(conn);
                        }
                        else
                        {
                            lock (conn)
                            {
                                if (!conn.InUse)
                                {
                                    conn.Dispose();
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
            Log.Debug(m => m("Finalizing ConnectionPool for {0}", EndPoint));
            Dispose(false);
        }
#endif

        IConnection IConnectionPool.Acquire()
        {
            return Acquire();
        }

        void IConnectionPool.Release(IConnection connection)
        {
            Release((T)connection);
        }

        IEnumerable<IConnection> IConnectionPool.Connections
        {
            get { return _store.ToArray(); }
        }

        public IServer Owner { get; set; }
    }
}
