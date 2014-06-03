using Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using Couchbase.Configuration.Client;

namespace Couchbase.IO.Strategies.Awaitable
{
    /// <summary>
    /// A pool for <see cref="SocketAwaitable"/> instances.
    /// </summary>
    internal sealed class SocketAwaitablePool
    {
        private readonly static ILog Log = LogManager.GetCurrentClassLogger();
        private readonly BufferAllocator _bufferAllocator = new BufferAllocator(50000 * 512, 512);
        private readonly ConcurrentQueue<SocketAwaitable> _pool = new ConcurrentQueue<SocketAwaitable>();
        private readonly Func<IConnectionPool, BufferAllocator, SocketAwaitable> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly IConnectionPool _connectionPool;
        private readonly object _lock = new object();
        private int _count;

        public SocketAwaitablePool(IConnectionPool connectionPool, Func<IConnectionPool, BufferAllocator, SocketAwaitable> socketAwaitableFactory)
        {
            _connectionPool = connectionPool;
            _factory = socketAwaitableFactory;
        }

        /// <summary>
        /// Acquires an <see cref="SocketAwaitable"/> instance from the pool.
        /// </summary>
        /// <returns>A <see cref="SocketAwaitable"/> object.</returns>
        /// <remarks>After the <see cref="PoolConfiguration.MinSize"/> is reached, the pool will grow to <see cref="PoolConfiguration.MaxSize"/>
        /// and any pending requests will then wait for a <see cref="SocketAwaitable"/> to be released back into the pool.
        /// </remarks>
        public SocketAwaitable Acquire()
        {
            SocketAwaitable socketAwaitable;
            if (_pool.TryDequeue(out socketAwaitable))
            {
                Log.Debug(m => m("Acquire existing socketAwaitable: {0} [{1}, {2}]", socketAwaitable.GetHashCode(), _count, _pool.Count));
                return socketAwaitable;
            }

            lock (_lock)
            {
                if (_count < _connectionPool.Configuration.MaxSize)
                {
                    socketAwaitable = _factory(_connectionPool, _bufferAllocator);
                    Log.Debug(m => m("Acquire new socketAwaitable: {0}", socketAwaitable.GetHashCode()));
                    Interlocked.Increment(ref _count);
                    return socketAwaitable;
                }
            }

            _autoResetEvent.WaitOne(_connectionPool.Configuration.WaitTimeout);

            Log.Debug(m => m("No socketAwaitable currently available. Trying again."));
            return Acquire();
        }

        /// <summary>
        /// Releases a <see cref="SocketAwaitable"/> instance back into the pool, so that it can be reused.
        /// </summary>
        /// <param name="socketAwaitable">A <see cref="SocketAwaitable"/> to release back into the pool.</param>
        public void Release(SocketAwaitable socketAwaitable)
        {
            Log.Debug(m => m("Releasing socketAwaitable: {0} [{1}, {2}]", socketAwaitable.GetHashCode(), _count, _pool.Count));

            _pool.Enqueue(socketAwaitable);
            _autoResetEvent.Set();
        }

        /// <summary>
        /// Resets a buffer on a <see cref="SocketAwaitable"/> instance.
        /// </summary>
        /// <param name="socketAwaitable"></param>
        /// <remarks>May be obsolete</remarks>
        public void ResetBuffer(SocketAwaitable socketAwaitable)
        {
            _bufferAllocator.ReleaseBuffer(socketAwaitable.EventArgs);
            _bufferAllocator.SetBuffer(socketAwaitable.EventArgs);
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

        public void Dispose()
        {
            throw new NotImplementedException();
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