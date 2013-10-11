using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached.Results;

namespace Enyim.Caching.Memcached.Protocol.Binary
{
	/// <summary>
	/// Provides a means of accessing internal protected methods of the SaslStep class to
	/// assemblies that are using the SaslStep class without changing the interface of that
	/// class.
	/// </summary>
	public static class SaslStepExtensions
	{
		
		/// <summary>
		/// Gets the buffer of the current SaslStep
		/// </summary>
		/// <param name="step">The current Sasl step.</param>
		/// <returns></returns>
		public static IList<ArraySegment<byte>> GetBuffer(this SaslStep step)
		{
			return step.GetBuffer();
		}
		
		/// <summary>
		/// Reads the response.
		/// </summary>
		/// <param name="step">The current SaslStep.</param>
		/// <param name="socket">The socket.to read from.</param>
		/// <returns></returns>
		public static IOperationResult ReadResponse(this SaslStep step, IPooledSocket socket)
		{
			return step.ReadResponse(socket);
		}
	}
}
