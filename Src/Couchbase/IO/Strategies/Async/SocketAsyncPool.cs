using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Couchbase.IO.Strategies.Awaitable;

namespace Couchbase.IO.Strategies.Async
{
    internal class SocketAsyncPool
    {
        private readonly ILog _log = LogManager.GetCurrentClassLogger();
        private readonly BufferAllocator _bufferAllocator = new BufferAllocator(500 * 512, 512);
        private readonly ConcurrentQueue<SocketAsyncEventArgs> _pool = new ConcurrentQueue<SocketAsyncEventArgs>();
        private readonly Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly IConnectionPool _connectionPool;
        private readonly object _lock = new object();
        private volatile int _count;
        private bool _disposed;

        public SocketAsyncPool(IConnectionPool connectionPool, Func<IConnectionPool, BufferAllocator, SocketAsyncEventArgs> socketAsyncFactory)
        {
            _connectionPool = connectionPool;
            _factory = socketAsyncFactory;
        }

        public SocketAsyncEventArgs Acquire()
        {
            SocketAsyncEventArgs socketAsync;
            if (_pool.TryDequeue(out socketAsync))
            {
                _log.Debug(m => m("Acquire existing SocketAsyncEventArgs: {0} [{1}, {2}]", socketAsync.GetHashCode(), _count, _pool.Count));
                return socketAsync;
            }

            lock (_lock)
            {
                if (_count < _connectionPool.Configuration.MaxSize)
                {
                    socketAsync = _factory(_connectionPool, _bufferAllocator);
                    _log.Debug(m => m("Acquire new SocketAsyncEventArgs: {0}", socketAsync.GetHashCode()));
                    Interlocked.Increment(ref _count);
                    return socketAsync;
                }
            }

            _autoResetEvent.WaitOne(_connectionPool.Configuration.WaitTimeout);

            _log.Debug(m => m("No SocketAsyncEventArgs currently available. Trying again."));
            return Acquire();
        }

        public void Release(SocketAsyncEventArgs socketAsync)
        {
            _log.Debug(m => m("Releasing SocketAsyncEventArgs: {0} [{1}, {2}]", socketAsync.GetHashCode(), _count, _pool.Count));

            _pool.Enqueue(socketAsync);
            _autoResetEvent.Set();
        }

        public int Count()
        {
            return _count;
        }

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
