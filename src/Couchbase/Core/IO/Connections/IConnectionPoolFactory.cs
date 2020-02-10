#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Creates a new <see cref="IConnectionPool"/>.
    /// </summary>
    internal interface IConnectionPoolFactory
    {
        /// <summary>
        /// Creates a new <see cref="IConnectionPool"/>.
        /// </summary>
        /// <param name="clusterNode"><see cref="ClusterNode"/> which will own this <see cref="IConnectionPool"/>.</param>
        IConnectionPool Create(ClusterNode clusterNode);
    }
}
