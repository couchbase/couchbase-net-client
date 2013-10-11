using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security;
using System.Threading;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;

namespace Couchbase
{
    internal class ConnectionPool : IResourcePool
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(ConnectionPool));
        private readonly IMemcachedNode _node;
        private readonly ISocketPoolConfiguration _config;
        private readonly object _syncObj = new object();
        private readonly Semaphore _semaphore;
        private readonly Queue<IPooledSocket> _queue;
        private readonly ISaslAuthenticationProvider _provider;
        private bool _disposed;
        private bool _isAlive;

        public ConnectionPool(IMemcachedNode node, ISocketPoolConfiguration config)
            : this(node, config, null)
        {
        }

        public ConnectionPool(IMemcachedNode node, ISocketPoolConfiguration config, ISaslAuthenticationProvider provider)
        {
            if (config.MinPoolSize < 0)
                throw new InvalidOperationException("MinPoolSize must be larger >= 0", null);
            if (config.MaxPoolSize < config.MinPoolSize)
                throw new InvalidOperationException("MaxPoolSize must be larger than MinPoolSize", null);
            if (config.QueueTimeout < TimeSpan.Zero)
                throw new InvalidOperationException("queueTimeout must be >= TimeSpan.Zero", null);

            _provider = provider;
            _node = node;
            _config = config;
            _semaphore = new Semaphore(config.MaxPoolSize, config.MaxPoolSize);
            _queue = new Queue<IPooledSocket>(config.MinPoolSize);
            _isAlive = true;
            PreAllocate(config.MinPoolSize);
        }

        public bool IsAlive { get { return _isAlive; } }

        //TODO inject this...i am purely repspecting the previous interface
        IPooledSocket Create()
        {
            Log.DebugFormat("Creating a socket on {0}", _node.EndPoint);
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = _config.ReceiveTimeout.Milliseconds,
                SendTimeout = _config.ReceiveTimeout.Milliseconds,
                NoDelay = true
            };

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.Connect(_node.EndPoint);

            var pooledSocket = new CouchbasePooledSocket(this, socket);
            if (_provider != null && !Authenticate(pooledSocket))
            {
                throw new SecurityException(String.Format("Authentication failed on {0}", _node.EndPoint));
            }
            Log.DebugFormat("Created socket Id={0} on {1}", _node.EndPoint, pooledSocket.InstanceId);
            return pooledSocket;
        }

        bool Authenticate(IPooledSocket socket)
        {
            var isAuthenticated = true;
            const int authContinue = 0x21;

            SaslStep step = new SaslStart(_provider);
            socket.Write(step.GetBuffer());
            while (!step.ReadResponse(socket).Success)
            {
                if (step.StatusCode == authContinue)
                {
                    step = new SaslContinue(_provider, step.Data);
                    socket.Write(step.GetBuffer());
                }
                else
                {
                    isAuthenticated = false;
                    break;
                }
            }
            return isAuthenticated;
        }

        void PreAllocate(int capacity)
        {
            Log.DebugFormat("PreAllocating {0} sockets on {1}", capacity, _node.EndPoint);
            for (var i = 0; i < capacity; i++)
            {
                _queue.Enqueue(Create());
            }
        }

        public IPooledSocket Acquire()
        {
            CheckDisposed();
            Log.DebugFormat("Acquiring socket on {0}", _node.EndPoint);
            _semaphore.WaitOne(_config.QueueTimeout);

            Monitor.Enter(_queue);
            var socket = _queue.Count > 0 ?
                _queue.Dequeue() :
                Create();
            Monitor.Exit(_queue);

            Log.DebugFormat("Acquired socket Id={0} on {1}", _node.EndPoint, socket.InstanceId);
            return socket;
        }

        public void Release(IPooledSocket resource)
        {
            Log.DebugFormat("Releasing socket Id={0} on {1}", resource.InstanceId, _node.EndPoint);
            Monitor.Enter(_syncObj);
            _queue.Enqueue(resource);
            Monitor.Exit(_syncObj);

            try
            {
                _semaphore.Release();
                Log.DebugFormat("Released socket Id={0} on {1}", resource.InstanceId, _node.EndPoint);
            }
            catch (ObjectDisposedException e)
            {
                Log.Warn("Cleaning up an orphaned socket", e);
                resource.Close();
            }
        }

        public bool Ping()
        {
            CheckDisposed();
            using (var socket = Create())
            {
                Log.DebugFormat("Pinging {0} on {1} ",
                    socket.IsConnected ? "succeeded" : "failed",
                    _node.EndPoint);

                return socket.IsConnected;
            }
        }

        public void Resurrect()
        {
            CheckDisposed();
            Log.DebugFormat("Resurrecting node on {0}", _node.EndPoint);
            Monitor.Enter(_syncObj);
            while (_queue.Count > 0)
            {
                var resource = _queue.Dequeue();
                resource.Close();
            }
            PreAllocate(_config.MinPoolSize);
            Monitor.Exit(_syncObj);
        }

        public void Close(IPooledSocket resource)
        {
            Log.DebugFormat("Closing socket Id={0} on {1}",
                resource.InstanceId,
                _node.EndPoint);

            resource.Close();
            _semaphore.Release();
        }

        public void Dispose()
        {
           Log.DebugFormat("Disposing {0} on {1}", this, _node.EndPoint);
           Dispose(true);
        }

        void Dispose(bool disposing)
        {
            Monitor.Enter(_syncObj);
            if (!_disposed)
            {
                while (_queue.Count > 0)
                {
                    var resource = _queue.Dequeue();
                    resource.Close();
                }
                _semaphore.Close();
            }
            if (disposing && !_disposed)
            {
                GC.SuppressFinalize(this);
                _disposed = true;
                _isAlive = false;
            }
            Monitor.Exit(_syncObj);
        }

        ~ConnectionPool()
        {
            Log.DebugFormat("Finalizing {0} on {1}", this, _node.EndPoint);
            Dispose(false);
        }

        void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
