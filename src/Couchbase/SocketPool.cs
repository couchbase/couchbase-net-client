using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
using System.Collections;
using Couchbase.Exceptions;
using Couchbase.Extensions;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Protocol.Binary;

namespace Couchbase
{
    internal class SocketPool : IResourcePool
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(SocketPool));
        private readonly IMemcachedNode _node;
        private readonly ISocketPoolConfiguration _config;
        private readonly object _syncObj = new object();
        private readonly Queue<IPooledSocket> _queue;
        private readonly List<IPooledSocket> _refs = new List<IPooledSocket>();
        private readonly ISaslAuthenticationProvider _provider;
        private volatile bool _disposed;
        private volatile bool _isAlive;
        private volatile bool _shutDownMode;
        private volatile int _outCount;

        public SocketPool(IMemcachedNode node, ISocketPoolConfiguration config)
            : this(node, config, null)
        {
        }

        public SocketPool(IMemcachedNode node, ISocketPoolConfiguration config, ISaslAuthenticationProvider provider)
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
            _queue = new Queue<IPooledSocket>(config.MaxPoolSize);
            _isAlive = true;
            PreAllocate(config.MinPoolSize);
        }

        public bool IsAlive { get { return _isAlive; } }

        private IPooledSocket Create()
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Creating a socket on {0}", _node.EndPoint);
            }
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = (int)_config.ReceiveTimeout.TotalMilliseconds,
                SendTimeout = (int)_config.ReceiveTimeout.TotalMilliseconds,
                NoDelay = true
            };
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, (int)_config.ReceiveTimeout.TotalMilliseconds);

            if (_config.LingerEnabled)
            {
                var lingerOptions = new LingerOption(_config.LingerEnabled, _config.LingerTime.Seconds);
                socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, lingerOptions);
            }
            socket.Connect(_node.EndPoint);
            if (_config.EnableTcpKeepAlives)
            {
                socket.SetKeepAlives(_config.EnableTcpKeepAlives,
                    _config.TcpKeepAliveTime,
                    _config.TcpKeepAliveInterval);
            }

            var pooledSocket = new CouchbasePooledSocket(this, socket);
            if (_provider != null && !Authenticate(pooledSocket))
            {
                throw new SecurityException(String.Format("Authentication failed on {0}", _node.EndPoint));
            }
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Created socket Id={0} on {1}", pooledSocket.InstanceId, _node.EndPoint);
            }
            _refs.Add(pooledSocket);
            return pooledSocket;
        }

        private bool Authenticate(IPooledSocket socket)
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

        private void PreAllocate(int capacity)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("PreAllocating {0} sockets on {1}", capacity, _node.EndPoint);
            }
            for (var i = 0; i < capacity; i++)
            {
                _queue.Enqueue(Create());
            }
        }

        public IPooledSocket Acquire()
        {
            IPooledSocket socket = null;
            lock (_syncObj)
            {
                while (_queue.Count == 0)
                {
                    if (_refs.Count() < _config.MaxPoolSize)
                    {
                        _queue.Enqueue(Create());
                        break;
                    }
                    if (!Monitor.Wait(_syncObj, _config.QueueTimeout))
                    {
                        break;
                    }
                }

                try
                {
                    socket = _queue.Dequeue();
                    socket.IsInUse = true;
                    if (socket.IsAlive)
                    {
                        socket.Reset();
                    }
                }
                catch (InvalidOperationException e)
                {
                    var sb = new StringBuilder();
                    sb.AppendFormat("Timeout occured while waiting for a socket on {0}.", _node.EndPoint);
                    sb.AppendFormat("Your current configuration for queueTmeout is {0}{1}",  _config.QueueTimeout, Environment.NewLine);
                    sb.AppendFormat("Your current configuration for maxPoolSize is {0}{1}", _config.MaxPoolSize, Environment.NewLine);
                    sb.AppendLine("Try increasing queueTimeout or increasing using maxPoolSize in your configuration.");
                    throw new QueueTimeoutException(sb.ToString(), e);
                }

                try
                {
                    if (!socket.IsAlive && (!_shutDownMode || _disposed))
                    {
                        socket.Close();
                        socket = Create();
                        socket.IsInUse = true;
                    }
                }
                catch (Exception)
                {
                    Release(socket);
                    throw;
                }
                if (_shutDownMode)
                {
                    var msg = String.Format("SocketPool for node {0} has shutdown.", _node.EndPoint);
                    throw new NodeShutdownException(msg);
                }
                Interlocked.Increment(ref _outCount);
                return socket;
            }
        }

        public void Release(IPooledSocket socket)
        {
            lock (_syncObj)
            {
                Interlocked.Decrement(ref _outCount);
                socket.IsInUse = false;
                if ((_disposed || _shutDownMode) && socket.IsAlive)
                {
                    socket.Close();
                }
                _queue.Enqueue(socket);
                Monitor.PulseAll(_syncObj);
            }
        }

        public void Close(IPooledSocket socket)
        {
            if (Log.IsDebugEnabled)
            {
                Log.DebugFormat("Closing socket Id={0} on {1}",
                    socket.InstanceId,
                    _node.EndPoint);
            }

            socket.Close();
        }

        public bool Ping()
        {
            using (var socket = Create())
            {
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Pinging {0} on {1} ",
                        socket.IsConnected ? "succeeded" : "failed",
                        _node.EndPoint);
                }

                return socket.IsConnected;
            }
        }

        public void Resurrect()
        {
            CheckDisposed();
            lock (_syncObj)
            {
                while (_queue.Count > 0)
                {
                    var socket = _queue.Dequeue();
                    socket.Close();
                }
                PreAllocate(_config.MinPoolSize);
            }
        }

        public void Dispose()
        {
            if (Log.IsWarnEnabled)
            {
                Log.WarnFormat("Disposing {0} on {1} using thread: {2}",
                    this, _node.EndPoint, Thread.CurrentThread.Name);
            }
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            const int maxAttempts = 13;
            _shutDownMode = true;
            if (!_disposed)
            {
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                    var itemsDisposed = 0;
                    var i = 2;
                    do
                    {
                        var timeout = (int) Math.Pow(2, i);
                        Thread.Sleep(timeout);

                        lock (_syncObj)
                        {
                            foreach (var socket in _refs.Where(x => x.IsAlive && !x.IsInUse))
                            {
                                if (Log.IsInfoEnabled)
                                {
                                    Log.DebugFormat("Gracefully closing {0} on server {1}", socket.InstanceId,
                                        _node.EndPoint);
                                }
                                socket.Close();
                                itemsDisposed++;
                            }
                        }

                        if (i != maxAttempts) continue;

                        lock (_syncObj)
                        {
                            foreach (var socket in _refs.Where(x => x.IsAlive))
                            {
                                if (Log.IsInfoEnabled)
                                {
                                    Log.DebugFormat("Force closing {0} on server {1}", socket.InstanceId, _node.EndPoint);
                                }
                                socket.Close();
                                if (Log.IsInfoEnabled)
                                {
                                    Log.DebugFormat("Force closed {0} on server {1}", socket.InstanceId, _node.EndPoint);
                                }
                                itemsDisposed++;
                            }
                        }
                    } while ((itemsDisposed < _refs.Count) && i++ < maxAttempts);

                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("Disposed {0} of {1} items.", itemsDisposed, _refs.Count);
                    }
                }
                _disposed = true;
            }
            else
            {
                foreach (var pooledSocket in _refs)
                {
                    try
                    {
                        if (pooledSocket.IsAlive)
                        {
                            pooledSocket.Dispose();
                        }
                    }
                    catch
                    {
                        //we want to catch any exceptions thrown during finalization
                        //we could log, but that could fail as well
                    }
                }
            }
            _isAlive = false;
        }

        ~SocketPool()
        {
            if(Log.IsDebugEnabled)
            {
                Log.DebugFormat("Finalizing {0}", GetType().Name);
            }
            Dispose(false);
        }

        private void CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }
    }
}
