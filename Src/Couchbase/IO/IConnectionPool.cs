using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.Remoting.Channels;
using Couchbase.Configuration.Client;
using Couchbase.Core;

namespace Couchbase.IO
{
    /// <summary>
    /// Represents a pool of TCP connections to a Couchbase Server node.
    /// </summary>
    internal interface IConnectionPool : IDisposable
    {
        /// <summary>
        /// Returns a <see cref="IConnection"/> the pool, creating a new one if none are available
        /// and the <see cref="PoolConfiguration.MaxSize"/> has not been reached.
        /// </summary>
        /// <returns>A TCP <see cref="IConnection"/> object to a Couchbase Server.</returns>
        IConnection Acquire();

        /// <summary>
        /// Releases an acquired <see cref="IConnection"/> object back into the pool so that it can be reused by another operation.
        /// </summary>
        /// <param name="connection">The <see cref="IConnection"/> to release back into the pool.</param>
        void Release(IConnection connection);

        /// <summary>
        /// Sets the initial state of the pool and adds the MinSize of <see cref="IConnection"/> object to the pool.
        /// After the <see cref="PoolConfiguration.MinSize"/> is reached, the pool will grow to <see cref="PoolConfiguration.MaxSize"/>
        /// and any pending requests will then wait for a <see cref="IConnection"/> to be released back into the pool.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Gets the number of <see cref="IConnection"/> within the pool, whether or not they are availabe or not.
        /// </summary>
        /// <returns></returns>
        int Count();

        /// <summary>
        /// The configuration passed into the pool when it is created. It has fields
        /// for MaxSize, MinSize, etc.
        /// </summary>
        PoolConfiguration Configuration { get; }

        /// <summary>
        /// The <see cref="IPEndPoint"/> of the server that the <see cref="IConnection"/>s are connected to.
        /// </summary>
        IPEndPoint EndPoint { get; set; }

        /// <summary>
        /// Returns a collection of <see cref="IConnection"/> objects.
        /// </summary>
        IEnumerable<IConnection> Connections { get; }

        /// <summary>
        /// Gets or sets the <see cref="IServer"/> instance which "owns" this pool.
        /// </summary>
        /// <value>
        /// The owner.
        /// </value>
        IServer Owner { get; set; }

        /// <summary>
        /// Gets a value indicating whether the pool failed to initialize properly.
        /// If for example, TCP connection to the server couldn't be made, then this
        /// would return false until the connection could be made (after the node went
        /// back online).
        /// </summary>
        /// <value>
        ///   <c>true</c> if initialization failed; otherwise, <c>false</c>.
        /// </value>
        bool InitializationFailed { get; }
    }
}
