using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Couchbase.Configuration;
using Couchbase.Results;
using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Extensions;

namespace Couchbase
{
    internal class CouchbaseNode : ICouchbaseNode
    {
        private readonly IHttpClient _client;
        private static readonly ILog Log = LogManager.GetLogger(typeof(CouchbaseNode));
        private readonly object _lockObj = new object();
        private readonly IPEndPoint _endpoint;
        private readonly ICouchbaseClientConfiguration _config;
        private readonly IResourcePool _pool;
        private bool _disposed = false;

        //TODO refactor CTORS if possible
        public CouchbaseNode(IPEndPoint endpoint, Uri couchApiBase, ICouchbaseClientConfiguration config, ISaslAuthenticationProvider provider)
        {
            var uriBuilder = new UriBuilder(couchApiBase);
            uriBuilder.Path = Path.Combine(uriBuilder.Path, "_design");

            _config = config;
            _client = config.CreateHttpClient(uriBuilder.Uri);
            _client.RetryCount = config.RetryCount;
            _endpoint = endpoint;
            _pool = new ConnectionPool(this, config.SocketPool, provider);
        }

        public CouchbaseNode(IPEndPoint endpoint, ISocketPoolConfiguration config)
        {
            _endpoint = endpoint;
            _pool = new ConnectionPool(this, config);
        }

        public CouchbaseNode(IPEndPoint endpoint, ISocketPoolConfiguration config, ISaslAuthenticationProvider provider)
        {
            _endpoint = endpoint;
            _pool = new ConnectionPool(this, config, provider);
        }

        public IPEndPoint EndPoint { get { return _endpoint; } }

        public IHttpClient Client { get { return _client; } }

        public bool IsAlive
        {
            get { return _pool.IsAlive; }
        }

        public IObserveOperationResult ExecuteObserveOperation(IObserveOperation op)
        {
            return Execute(op) as IObserveOperationResult;
        }

        public bool Ping()
        {
            CheckDiposed();
            Monitor.Enter(_lockObj);
            var isAlive = IsAlive;

            if (!isAlive && _pool.Ping())
            {
                _pool.Resurrect();
                isAlive = _pool.IsAlive;
            }

            Monitor.Enter(_lockObj);
            return isAlive;
        }

        public IOperationResult Execute(IOperation op)
        {
            IOperationResult result = new BinaryOperationResult();
            IPooledSocket socket = null;
            try
            {
                socket = _pool.Acquire();
                var buffers = op.GetBuffer();
                socket.Write(buffers);

                result = op.ReadResponse(socket);
                if (result.Success)
                {
                    result.Pass();
                }
            }
            catch (IOException e)
            {
                const string msg = "Exception reading response";
                Log.Error(msg, e);
                result.Fail(msg, e);
            }
            catch (ObjectDisposedException e)
            {
                const string msg = "Could not acquire socket, likely a failover or rebalance scenario.";
                Log.Warn(msg, e);
                result.Fail(msg, e);
            }
            finally
            {
                if (socket != null)
                {
                    socket.Dispose();
                }
            }
            return result;
        }

        public bool ExecuteAsync(IOperation op, Action<bool> next)
        {
            IPooledSocket socket = null;
            var result = false;
            try
            {
                socket = _pool.Acquire();
                var buffers = op.GetBuffer();
                socket.Write(buffers);

                result = op.ReadResponseAsync(socket, readSuccess =>
                {
                    socket.Dispose();
                    next(readSuccess);
                });
            }
            catch (IOException e)
            {
                Log.Error(e);
            }
            finally
            {
                if (socket != null)
                {
                    socket.Dispose();
                }
            }
            return result;
        }

        public event Action<IMemcachedNode> Failed;

        public void Dispose()
        {
            Log.DebugFormat("Disposing {0}", this);
            CheckDiposed();
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _pool.Dispose();
            }
            if (disposing && !_disposed)
            {
                GC.SuppressFinalize(this);
                _disposed = true;
            }
        }

        ~CouchbaseNode()
        {
            Log.DebugFormat("Finalizing {0}", this);
            Dispose(false);
        }

        //TODO make this private
        public void CheckDiposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("CoucbaseNode2");
            }
        }
    }
}
