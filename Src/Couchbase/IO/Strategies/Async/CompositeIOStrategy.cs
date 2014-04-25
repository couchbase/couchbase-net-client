using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies.Async
{
    internal sealed class CompositeIOStrategy : IOStrategy
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentQueue<IOStrategy> _pool = new ConcurrentQueue<IOStrategy>();
        private readonly object _lock = new object();
        private int _count;
        private readonly int _maxSize = 10;
        private readonly Func<IConnectionPool, IOStrategy> _factory;
        private readonly TimeSpan _waitTimeout;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private IConnectionPool _connectionPool;

        public CompositeIOStrategy(int maxSize, TimeSpan waitTimeout, Func<IConnectionPool, IOStrategy> factory, IConnectionPool connectionPool)
        {
            _maxSize = maxSize;
            _factory = factory;
            _waitTimeout = waitTimeout;
            _connectionPool = connectionPool;
           // Initialize();
        }

        public IOStrategy Acquire()
        {
            IOStrategy iOStrategy = null;
            if (_pool.TryDequeue(out iOStrategy))
            {
                _log.Debug(m=>m("aquire existing IOStrategy {0}", iOStrategy.GetHashCode()));
                return iOStrategy;
            }

            lock (_lock)
            {
                if (_count < _maxSize)
                {
                    iOStrategy = _factory(ConnectionPool);
                    _log.Debug(m => m("create new IOStrategy {0}", iOStrategy.GetHashCode()));

                    Interlocked.Increment(ref _count );
                    return iOStrategy;
                }
            }
            _autoResetEvent.WaitOne(_waitTimeout);

            _log.Debug(m => m("No SocketAsyncEventArgs currently available. Trying again."));
            return Acquire();
        }

        public void Release(IOStrategy strategy)
        {
            _log.Debug(m => m("Releasing strategy: {0} [{1}, {2}]", strategy.GetHashCode(), _count, _pool.Count));

            _pool.Enqueue(strategy);
            _autoResetEvent.Set();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation, IConnection connection)
        {
            throw new NotImplementedException();
        }

        public IOperationResult<T> Execute<T>(IOperation<T> operation)
        {
            var iOStrategy = Acquire();
            _log.Debug(m=>m("Executing operation on thread {0}", Thread.CurrentThread.ManagedThreadId));
            var result = iOStrategy.Execute(operation);
            Release(iOStrategy);
            return result;
        }

        public IPEndPoint EndPoint
        {
            get { return ConnectionPool.EndPoint; }
        }

        public IConnectionPool ConnectionPool
        {
            get { return _connectionPool; }
        }

        public void Initialize()
        {
            do
            {
                _pool.Enqueue(_factory(_connectionPool));
                Interlocked.Increment(ref _count);
            }
            while (_pool.Count < _connectionPool.Configuration.MinSize);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public Authentication.SASL.ISaslMechanism SaslMechanism
        {
            set { throw new NotImplementedException(); }
        }
    }
}
