using System;
using System.Collections.Generic;
using System.Text;

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
        void Complete(byte[] response);
    }
}
