namespace Couchbase.Core.Configuration.Server.Streaming
{
    /// <summary>
    /// An interface for creating <see cref="HttpClusterMap"/> instances.
    /// </summary>
    internal interface IHttpClusterMapFactory
    {
        /// <summary>
        /// Creates a new <see cref="HttpClusterMapBase"/> instance.
        /// </summary>
        /// <param name="context">The owning <see cref="ClusterContext"/>.</param>
        /// <returns>A <see cref="HttpClusterMapBase"/> for fetching configs.</returns>
        HttpClusterMapBase Create(ClusterContext context);
    }
}
