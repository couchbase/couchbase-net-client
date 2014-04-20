using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;

namespace Couchbase.IO.Strategies.Awaitable
{
    internal sealed class AwaitableSocketPool
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private readonly BufferAllocator _bufferAllocator = new BufferAllocator(50000 * 512, 512);
        private readonly ConcurrentQueue<SocketAwaitable> _pool = new ConcurrentQueue<SocketAwaitable>();
        private readonly Func<IConnectionPool, BufferAllocator, SocketAwaitable> _factory;
        private readonly AutoResetEvent _autoResetEvent = new AutoResetEvent(false);
        private readonly IConnectionPool _connectionPool;
        private readonly object _lock = new object();
        private int _count;
        private bool _disposed;

        public AwaitableSocketPool(IConnectionPool connectionPool, Func<IConnectionPool, BufferAllocator, SocketAwaitable> socketAwaitableFactory)
        {
            _connectionPool = connectionPool;
            _factory = socketAwaitableFactory;
        }

        public SocketAwaitable Acquire()
        {
            SocketAwaitable socketAwaitable;
            if (_pool.TryDequeue(out socketAwaitable))
            {
                Log.Debug(m=>m("Acquire existing socketAwaitable: {0} [{1}, {2}]", socketAwaitable.GetHashCode(), _count, _pool.Count));
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

        public void Release(SocketAwaitable socketAwaitable)
        {
            Log.Debug(m => m("Releasing socketAwaitable: {0} [{1}, {2}]", socketAwaitable.GetHashCode(), _count, _pool.Count));

            _pool.Enqueue(socketAwaitable);
            _autoResetEvent.Set();
        }

        public void ResetBuffer(SocketAwaitable socketAwaitable)
        {
            _bufferAllocator.ReleaseBuffer(socketAwaitable.EventArgs);
            _bufferAllocator.SetBuffer(socketAwaitable.EventArgs);
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
