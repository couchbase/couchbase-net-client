using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.IO.Operations;

namespace Couchbase.IO.Strategies.Async
{
    internal sealed class CompositeIOStrategy : IOStrategy
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly ConcurrentQueue<IOStrategy> _pool = new ConcurrentQueue<IOStrategy>();
        private readonly object _lock = new object();
        private int _count;
        private readonly int _maxSize = 10;
        private readonly Func<IConnectionPool, IOStrategy> _factory;
        private readonly TimeSpan _waitTimeout;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly IConnectionPool _connectionPool;

        public CompositeIOStrategy(int maxSize, TimeSpan waitTimeout, Func<IConnectionPool, IOStrategy> factory, IConnectionPool connectionPool)
        {
            _maxSize = maxSize;
            _factory = factory;
            _waitTimeout = waitTimeout;
            _connectionPool = connectionPool;
        }

        public IOStrategy Acquire()
        {
            IOStrategy iOStrategy;
            if (_pool.TryDequeue(out iOStrategy))
            {
                Log.Debug(m=>m("aquire existing IOStrategy {0}", iOStrategy.GetHashCode()));
                return iOStrategy;
            }

            lock (_lock)
            {
                if (_count < _maxSize)
                {
                    iOStrategy = _factory(ConnectionPool);
                    Log.Debug(m => m("create new IOStrategy {0}", iOStrategy.GetHashCode()));

                    Interlocked.Increment(ref _count );
                    return iOStrategy;
                }
            }
            _autoResetEvent.WaitOne(_waitTimeout);

            Log.Debug(m => m("No SocketAsyncEventArgs currently available. Trying again."));
            return Acquire();
        }

        public void Release(IOStrategy strategy)
        {
            Log.Debug(m => m("Releasing strategy: {0} [{1}, {2}]", strategy.GetHashCode(), _count, _pool.Count));

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
            Log.Debug(m=>m("Executing operation on thread {0}", Thread.CurrentThread.ManagedThreadId));
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


        public bool IsSecure
        {
            get { throw new NotImplementedException(); }
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