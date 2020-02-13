using System;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Monitors a <see cref="IConnectionPool"/> and adjusts its size based on load.
    /// </summary>
    internal interface IConnectionPoolScaleController : IDisposable
    {
        /// <summary>
        /// Start the scale controller.
        /// </summary>
        /// <param name="connectionPool">The <see cref="IConnectionPool"/> to scale.</param>
        void Start(IConnectionPool connectionPool);
    }
}
