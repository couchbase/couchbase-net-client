using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using Enyim.Caching.Memcached;

namespace Couchbase
{
	internal class CouchbasePooledSocket : IPooledSocket
	{
		private static readonly Enyim.Caching.ILog Log = Enyim.Caching.LogManager.GetLogger(typeof(CouchbasePooledSocket));
		private readonly object _syncObj = new object();
		private readonly IResourcePool _pool;
		private readonly Socket _socket;
		private readonly NetworkStream _stream;
		private readonly Guid _instanceId;
		private volatile bool _disposed;
		private volatile bool _isAlive;
	    private volatile bool _isInUse;

		/// <summary>
		/// Will be removed in later versions
		/// </summary>
		[Obsolete("Will be removed in later versions")]
		private AsyncSocketHelper2 _helper2;

		public CouchbasePooledSocket(IResourcePool pool, Socket socket)
		{
			_isAlive = true;
			_pool = pool;
			_socket = socket;
			_instanceId = Guid.NewGuid();
			_stream = new NetworkStream(socket, true);
		}

		public Guid InstanceId { get { return _instanceId; } }

		public bool IsConnected { get { return _socket.Connected; } }

		public int Available { get { return _socket.Available; } }

		public bool IsAlive { get { return _isAlive; } }

        public bool IsInUse { get { return _isInUse; } set { _isInUse = value; } }

	    void OperationTimeout(object state)
	    {
	        var socket = state as IPooledSocket;
	        if (socket != null)
	        {
	            if (Log.IsDebugEnabled)
	            {
	                Log.DebugFormat("Operation timeout.");
	            }
                socket.Close();
	        }
	    }

		public int ReadByte()
		{
            CheckDisposed();

			try
			{
				return _stream.ReadByte();
			}
			catch (Exception)
			{
			    _isAlive = false;
				throw;
			}
		}

		public void Read(byte[] buffer, int offset, int count)
		{
			CheckDisposed();

			var read = 0;
		    var shouldRead = count;

		    try
		    {
		        using (new Timer(OperationTimeout, this, _socket.ReceiveTimeout, 0))
		        {
                    while (read < count)
                    {
                        var current = _stream.Read(buffer, offset, shouldRead);
                        if (current < 1)
                            continue;

                        read += current;
                        offset += current;
                        shouldRead -= current;
                    }
		        }
		    }
		    catch (Exception)
		    {
                _isAlive = false;
		        throw;
		    }
		}

		public void Write(byte[] data, int offset, int length)
		{
			CheckDisposed();

		    SocketError status;
		    _socket.Send(data, offset, length, SocketFlags.None, out status);

		    if (status != SocketError.Success)
		    {
                _isAlive = false;
		        ThrowHelper.ThrowSocketWriteError(_socket.RemoteEndPoint, status);
		    }
		}

		public void Write(IList<ArraySegment<byte>> buffers)
		{
			CheckDisposed();

			SocketError status;
			_socket.Send(buffers, SocketFlags.None, out status);

			if (status != SocketError.Success)
			{
                _isAlive = false;
				ThrowHelper.ThrowSocketWriteError(_socket.RemoteEndPoint, status);
			}
		}

	   [Obsolete("Will be removed in later versions")]
	   public bool ReceiveAsync(AsyncIOArgs p)
	   {
		   CheckDisposed();

		   if (!IsAlive)
		   {
			   p.Fail = true;
			   p.Result = null;
			   return false;
		   }
		   if (_helper2 == null)
		   {
			   _helper2 = new AsyncSocketHelper2(this);
		   }
		   return _helper2.Read(p);
	   }

		[Obsolete("Will be removed in later versions")]
		internal Socket Socket{ get { return _socket; }}

		[Obsolete("Will be removed in later versions")]
		public Action<IPooledSocket> CleanupCallback
		{
			get { throw new NotImplementedException(); }
			set { throw new NotImplementedException(); }
		}

		public void Reset()
		{
			throw new NotImplementedException();
		}

		[Obsolete("Will be removed in later versions")]
		public void Release()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Releases the resource back into the pool
		/// </summary>
		public void Dispose()
		{
			if (Log.IsDebugEnabled)
			{
				Log.DebugFormat("Closing socket {0}", _instanceId);
			}
			_pool.Release(this);
		}

		~CouchbasePooledSocket()
		{
			Close(false);
		}

		void Close(bool disposing)
		{
			lock (_syncObj)
			{
				_isInUse = false;
				if (disposing && !_disposed)
				{
					GC.SuppressFinalize(this);
				}
				if (!_disposed)
				{
					if (_stream != null)
					{
						_stream.Close();
						_stream.Dispose();
					}
					_disposed = true;
                    _isAlive = false;
				}
			}
		}

		/// <summary>
		/// Closes the connection and removes the resource from the pool
		/// </summary>
		public void Close()
		{
			Close(true);
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
