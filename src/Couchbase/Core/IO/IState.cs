using System;
using System.Buffers;

namespace Couchbase.Core.IO
{
    /// <summary>
    /// Represents a Memcached request in flight.
    /// </summary>
    internal interface IState : IDisposable
    {
        /// <summary>
        /// Completes the specified Memcached response.
        /// </summary>
        /// <param name="response">The Memcached response packet.</param>
        /// <remarks>
        /// Ownership of the data is transferred by this call, and the callee is responsible for releasing the memory,
        /// unless an exception is thrown.
        /// </remarks>
        void Complete(IMemoryOwner<byte> response);
    }
}
