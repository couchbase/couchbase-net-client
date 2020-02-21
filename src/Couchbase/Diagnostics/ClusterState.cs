namespace Couchbase.Diagnostics
{
    /// <summary>
    /// The current state of the <see cref="Cluster"/> instance.
    /// </summary>
    public enum ClusterState
    {
        /// <summary>
        /// All nodes and their sockets are reachable.
        /// </summary>
        Online,

        /// <summary>
        /// At least one socket per service is reachable.
        /// </summary>
        Degraded,

        /// <summary>
        /// Not even one socket per service is reachable.
        /// </summary>
        Offline
    }
}
