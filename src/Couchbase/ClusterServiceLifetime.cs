namespace Couchbase
{
    /// <summary>
    /// Represents the scope used for a custom cluster service.
    /// </summary>
    public enum ClusterServiceLifetime
    {
        /// <summary>
        /// A new service will be created for each request for the service.
        /// </summary>
        Transient,

        /// <summary>
        /// A single service will be used for the lifetime of the cluster.
        /// </summary>
        Cluster
    }
}
