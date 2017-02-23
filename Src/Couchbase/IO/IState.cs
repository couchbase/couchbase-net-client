using System;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a Memcached request in flight.
    /// </summary>
    internal interface IState
    {
        /// <summary>
        /// Completes the specified Memcached response.
        /// </summary>
        /// <param name="response">The Memcached response packet.</param>
        void Complete(byte[] response);
    }
}
