using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;

namespace Couchbase.IO
{
    internal class  DefaultConnectionPool : IConnectionPool
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentQueue<IConnection> _store = new ConcurrentQueue<IConnection>();
        private readonly Func<IConnectionPool, IConnection> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly PoolConfiguration _configuration;
        private readonly object _lock = new object();
        private int _count;
        private bool _disposed;

        public DefaultConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint) 
            : this(configuration, endPoint, DefaultConnectionFactory.GetDefault())
        {
        }

        public DefaultConnectionPool(PoolConfiguration configuration, IPEndPoint endPoint, Func<IConnectionPool, IConnection> factory)
        {
            _configuration = configuration;
            _factory = factory;
            EndPoint = endPoint;
        }

        public PoolConfiguration Configuration
        {
            get { return _configuration; }
        }

        public IPEndPoint EndPoint { get; set; }

        public IEnumerable<IConnection> Connections
        {
            get { return _store.ToArray(); }
        }

        public int Count()
        {
            return _count;
        }

        public void Initialize()
        {
            do
            {
                _store.Enqueue(_factory(this));
                Interlocked.Increment(ref _count);
            }
            while (_store.Count < _configuration.MinSize);
        }

        public IConnection Acquire()
        {
            IConnection connection;

            if (_store.TryDequeue(out connection))
            {
                Log.Debug(m=>m("Acquire existing: {0}", connection.Identity));
                return connection;
            }

            lock (_lock)
            {
                if (_count < _configuration.MaxSize)
                {
                    connection = _factory(this);

                    Log.Debug(m=>m("Acquire new: {0}", connection.Identity));
                    Interlocked.Increment(ref _count);
                    return connection;
                }
            }

            _autoResetEvent.WaitOne(_configuration.WaitTimeout);

            Log.Debug(m=>m("No connections currently available. Trying again."));
            return Acquire();
        }

        public void Release(IConnection connection)
        {
            Log.Debug(m=>m("Releasing: {0}", connection.Identity));

            _store.Enqueue(connection);
            _autoResetEvent.Set();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                while (_store.Count > 0)
                {
                    IConnection connection;
                    if (_store.TryDequeue(out connection))
                    {
                        connection.Dispose();
                    }
                }
            }
        }

        ~DefaultConnectionPool()
        {
            Dispose(false);
        }
    }
}
