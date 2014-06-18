using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;

namespace Couchbase.IO
{
    internal class ConnectionPool<T> : IConnectionPool<T> where T : class, IConnection
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentQueue<T> _store = new ConcurrentQueue<T>();
        private readonly Func<ConnectionPool<T>, IByteConverter, T> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly PoolConfiguration _configuration;
        private readonly object _lock = new object();
        private readonly IByteConverter _converter;
        private int _count;
        private bool _disposed;

        public ConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint)
            : this(configuration, endPoint, DefaultConnectionFactory.GetGeneric<T>(), new ManualByteConverter())
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
        /// Gets the number of <see cref="IConnection"/> within the pool, whether or not they are availabe or not.
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
            do
            {
                _store.Enqueue(_factory(this, _converter));
                Interlocked.Increment(ref _count);
            } while (_store.Count < _configuration.MinSize);
        }

        /// <summary>
        /// Returns a <see cref="IConnection"/> the pool, creating a new one if none are available
        /// and the <see cref="PoolConfiguration.MaxSize"/> has not been reached.
        /// </summary>
        /// <returns>A TCP <see cref="IConnection"/> object to a Couchbase Server.</returns>
        public T Acquire()
        {
            T connection;

            if (_store.TryDequeue(out connection))
            {
                Log.Debug(m => m("Acquire existing: {0} - [{1}, {2}]", connection.Identity, _store.Count, _count));
                return connection;
            }

            lock (_lock)
            {
                if (_count < _configuration.MaxSize)
                {
                    connection = _factory(this, _converter);

                    Log.Debug(m => m("Acquire new: {0} - [{1}, {2}]", connection.Identity, _store.Count, _count));
                    Interlocked.Increment(ref _count);
                    return connection;
                }
            }

            _autoResetEvent.WaitOne(_configuration.WaitTimeout);

            Log.Debug(m => m("No connections currently available. Trying again."));
            return Acquire();
        }

        /// <summary>
        /// Releases an acquired <see cref="IConnection"/> object back into the pool so that it can be reused by another operation.
        /// </summary>
        /// <param name="connection">The <see cref="IConnection"/> to release back into the pool.</param>
        public void Release(T connection) 
        {
            Log.Debug(m => m("Releasing: {0}", connection.Identity));

            _store.Enqueue(connection);
            _autoResetEvent.Set();
        }

        /// <summary>
        /// Removes and disposes all <see cref="IConnection"/> objects in the pool.
        /// </summary>
        public void Dispose()
        {
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
                if (_store == null) return;
                while (_store.Count > 0)
                {
                    T connection;
                    if (_store.TryDequeue(out connection))
                    {
                        connection.Dispose();
                    }
                }
            }
        }

        ~ConnectionPool()
        {
            Dispose(false);
        }

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
    }
}
