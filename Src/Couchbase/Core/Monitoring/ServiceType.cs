namespace Couchbase.Core.Monitoring
{
    public enum ServiceType
    {
        /// <summary>
        /// The key/value service.
        /// </summary>
        KeyValue,

        /// <summary>
        /// The map/reduce view service.
        /// </summary>
        Views,

        /// <summary>
        /// The N1QL query service.
        /// </summary>
        Query,

        /// <summary>
        /// The full text search service.
        /// </summary>
        Search,

        /// <summary>
        /// The cluster configuration service.
        /// </summary>
        Config,

        /// <summary>
        /// The analytics service.
        /// </summary>
        Analytics
    }
}
