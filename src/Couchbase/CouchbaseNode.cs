using System;
using System.IO;
using System.Net;
using System.Threading;
using Couchbase.Configuration;
using Couchbase.Exceptions;
using Couchbase.Results;
using Enyim;
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

		public CouchbaseNode(IPEndPoint endpoint, Uri couchApiBase, ICouchbaseClientConfiguration config, ISaslAuthenticationProvider provider)
		{
			var uriBuilder = new UriBuilder(couchApiBase);
			uriBuilder.Path = Path.Combine(uriBuilder.Path, "_design");

			_config = config;
			_client = config.CreateHttpClient(uriBuilder.Uri);
			_client.RetryCount = config.RetryCount;
			_endpoint = endpoint;
			_pool = new SocketPool(this, config.SocketPool, provider);
		}

		public CouchbaseNode(IPEndPoint endpoint, ISocketPoolConfiguration config)
		{
			_endpoint = endpoint;
			_pool = new SocketPool(this, config);
		}

		public CouchbaseNode(IPEndPoint endpoint, ISocketPoolConfiguration config, ISaslAuthenticationProvider provider)
		{
			_endpoint = endpoint;
			_pool = new SocketPool(this, config, provider);
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
			    if (Log.IsDebugEnabled)
			    {
			        Log.DebugFormat("Start execute {0} with {1}", op.Key, socket.InstanceId);
			    }
				var buffers = op.GetBuffer();
				socket.Write(buffers);

				result = op.ReadResponse(socket);
				if (result.Success)
				{
					result.Pass();
				}
			}
			catch (NodeShutdownException e)
			{
				const string msg = "Node Shutdown.";
				Log.DebugFormat("m:{0} i:{1}\n{2}", msg, op.Key, e);
				result.Fail(msg, e);
				result.StatusCode = StatusCode.NodeShutdown.ToInt();
			}
			catch (QueueTimeoutException e)
			{
				const string msg = "Queue Timeout.";
				Log.ErrorFormat("m:{0} i:{1}\n{2}", msg, op.Key, e);
				result.Fail(msg, e);
				result.StatusCode = StatusCode.SocketPoolTimeout.ToInt();
			}
			catch (IOException e)
			{
				const string msg = "Exception reading response";
                Log.ErrorFormat("m:{0} s:{1} i:{2}\n{3}", msg, op.Key,
                    socket == null ? Guid.Empty : socket.InstanceId, e);
				result.Fail(msg, e);
				if (result.StatusCode == null ||
					result.StatusCode == StatusCode.Success.ToInt())
				{
					result.StatusCode = StatusCode.InternalError.ToInt();
				}
			}
			catch (Exception e)
			{
				const string msg = "Operation failed.";
				Log.ErrorFormat("m:{0} s:{1} i:{2}\n{3}", msg, op.Key,
                    socket == null ? Guid.Empty : socket.InstanceId, e);
				result.Fail(msg, e);
				if (result.StatusCode == null ||
					result.StatusCode == StatusCode.Success.ToInt())
				{
					result.StatusCode = StatusCode.InternalError.ToInt();
				}
			}
			finally
			{
				if (socket != null)
				{
                    if (Log.IsDebugEnabled)
                    {
                        Log.DebugFormat("End execute {0} with {1}", op.Key, socket.InstanceId);
                    }
					_pool.Release(socket);
				}
			}
			if (Log.IsDebugEnabled)
			{
				const string msg = "Operation {0} :i {1} n: {2} t: {3} m: {4} sc: {5} r: {6}";
				Log.DebugFormat(msg, result.Success ?
                    op.RetryAttempts > 0 ? "succeeded*" : "succeeded" :
                    op.RetryAttempts > 0 ? "failed*" : "failed",
					op.Key,
					_endpoint, Thread.CurrentThread.Name,
					result.Message,
					Enum.GetName(typeof (StatusCode), result.StatusCode ?? 0),
					op.RetryAttempts);
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
					_pool.Release(socket);
				}
			}
			return result;
		}

		public event Action<IMemcachedNode> Failed;

		public void Dispose()
		{
			Log.DebugFormat("Disposing {0} - {1}", this, EndPoint);
			CheckDiposed();
			Dispose(true);
		}

		void Dispose(bool disposing)
		{
			if (!_disposed && _pool != null)
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
			Dispose(false);
		}

		void CheckDiposed()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(GetType().FullName);
			}
		}
	}
}
