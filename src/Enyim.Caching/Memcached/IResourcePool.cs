using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Enyim.Caching.Memcached
{
	/// <summary>
	/// Provides an interface for implementing pools of disposable IPooledSocket objects.
	/// </summary>
	public interface IResourcePool : IDisposable
	{
		/// <summary>
		/// Acquires an object from the pool.
		/// </summary>
		/// <returns>A pooled object</returns>
		IPooledSocket Acquire();

		/// <summary>
		/// Releases an object back into the pool.
		/// </summary>
		/// <param name="resource"></param>
		void Release(IPooledSocket resource);

		/// <summary>
		/// Closes all of the underlying resources held by the pooled object.
		/// </summary>
		/// <param name="resource"></param>
		void Close(IPooledSocket resource);

		/// <summary>
		/// Checks to see if the underlying resource is still "alive".
		/// </summary>
		bool IsAlive { get; }

		/// <summary>
		/// Pulls a socket from the pool and checks it's connection status.
		/// </summary>
		/// <returns></returns>
		bool Ping();

		/// <summary>
		/// Tries to recreate the underlying pool of objects.
		/// </summary>
		void Resurrect();
	}
}
