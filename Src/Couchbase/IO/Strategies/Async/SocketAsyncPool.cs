using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies.Async
{
    /// <summary>
    /// A pool for <see cref="SocketAsyncEventArgs"/> objects.
    /// </summary>
    internal sealed class SocketAsyncPool
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly BufferAllocator _bufferAllocator = new BufferAllocator(500 * 512, 512);
        private readonly ConcurrentQueue<SocketAsyncEventArgs> _pool = new ConcurrentQueue<SocketAsyncEventArgs>();
        private readonly Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly IConnectionPool _connectionPool;
        private readonly object _lock = new object();
        private int _count;
        private bool _disposed;

        public SocketAsyncPool(IConnectionPool connectionPool, Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> socketAsyncFactory)
        {
            _connectionPool = connectionPool;
            _factory = socketAsyncFactory;
        }

        /// <summary>
        /// Acquires an <see cref="SocketAsyncEventArgs"/> instance from the pool.
        /// </summary>
        /// <returns>A <see cref="SocketAsyncEventArgs"/> object.</returns>
        /// <remarks>After the <see cref="PoolConfiguration.MinSize"/> is reached, the pool will grow to <see cref="PoolConfiguration.MaxSize"/>
        /// and any pending requests will then wait for a <see cref="SocketAwaitable"/> to be released back into the pool.
        /// </remarks>
        public SocketAsyncEventArgs Acquire()
        {
            SocketAsyncEventArgs socketAsync;
            if (_pool.TryDequeue(out socketAsync))
            {
                Log.Debug(m => m("Acquire existing SocketAsyncEventArgs: {0} [{1}, {2}]", socketAsync.GetHashCode(), _count, _pool.Count));
                return socketAsync;
            }

            lock (_lock)
            {
                if (_count < _connectionPool.Configuration.MaxSize)
                {
                    socketAsync = _factory(_connectionPool, _bufferAllocator);
                    Log.Debug(m => m("Acquire new SocketAsyncEventArgs: {0}", socketAsync.GetHashCode()));
                    Interlocked.Increment(ref _count);
                    return socketAsync;
                }
            }

            _autoResetEvent.WaitOne(_connectionPool.Configuration.WaitTimeout);

            Log.Debug(m => m("No SocketAsyncEventArgs currently available. Trying again."));
            return Acquire();
        }

        /// <summary>
        /// Releases a <see cref="SocketAsyncEventArgs"/> instance back into the pool, so that it can be reused.
        /// </summary>
        /// <param name="socketAsync">A <see cref="SocketAsyncEventArgs"/> to release back into the pool.</param>
        public void Release(SocketAsyncEventArgs socketAsync)
        {
            Log.Debug(m => m("Releasing SocketAsyncEventArgs: {0} [{1}, {2}]", socketAsync.GetHashCode(), _count, _pool.Count));

            _pool.Enqueue(socketAsync);
            _autoResetEvent.Set();
        }

        /// <summary>
        /// The total count of <see cref="SocketAwaitable"/> allocated.
        /// </summary>
        /// <returns>The total count of <see cref="SocketAwaitable"/> allocated.</returns>
        public int Count()
        {
            return _count;
        }

        /// <summary>
        /// Initializes the pool to the <see cref="PoolConfiguration.MinSize"/>
        /// </summary> provided in the configuration.
        public void Initialize()
        {
            do
            {
                _pool.Enqueue(_factory(_connectionPool, _bufferAllocator));
                Interlocked.Increment(ref _count);
            }
            while (_pool.Count < _connectionPool.Configuration.MinSize);
        }

        /// <summary>
        /// Releases and disposes all <see cref="SocketAsyncEventArgs"/> associated with this pool.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }

                if (_pool == null) return;
                foreach (SocketAsyncEventArgs socketAsyncEventArgs in _pool)
                {
                    socketAsyncEventArgs.Dispose();
                }
            }
            _disposed = true;
        }

        ~SocketAsyncPool()
        {
            Dispose(false);
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