using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Threading;
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
		private readonly ISaslAuthenticationProvider _provider;
		private bool _disposed;
		private bool _isAlive;

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
			Log.DebugFormat("Acquiring socket on {0}", _node.EndPoint);
			lock (_syncObj)
			{
				while (_queue.Count == 0)
				{
					Monitor.Wait(_syncObj, _config.QueueTimeout);
				}
				var socket = _queue.Dequeue();
				Log.DebugFormat("Acquired socket Id={0} on {1}", _node.EndPoint, socket.InstanceId);
				return socket;
			}
		}

		public void Release(IPooledSocket socket)
		{
			Log.DebugFormat("Releasing socket Id={0} on {1}", socket.InstanceId, _node.EndPoint);
			lock (_syncObj)
			{
				if (!socket.IsAlive)
				{
					socket.Close();
					socket = Create();
				}
				_queue.Enqueue(socket);
				Monitor.PulseAll(_syncObj);
			}
			Log.DebugFormat("Released socket Id={0} on {1}", socket.InstanceId, _node.EndPoint);
		}

		public void Close(IPooledSocket socket)
		{
			Log.DebugFormat("Closing socket Id={0} on {1}",
			  socket.InstanceId,
			   _node.EndPoint);

			socket.Close();
		}

		public bool Ping()
		{
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
			Log.DebugFormat("Disposing {0} on {1}", this, _node.EndPoint);
			Dispose(true);
		}

		void Dispose(bool disposing)
		{
			lock (_syncObj)
			{
				if (!_disposed)
				{
					while (_queue.Count > 0)
					{
						var socket = _queue.Dequeue();
						socket.Close();
					}
				}
				if (disposing && !_disposed)
				{
					GC.SuppressFinalize(this);
					_disposed = true;
					_isAlive = false;
				}
			}
		}

		~SocketPool()
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
