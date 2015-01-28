namespace Couchbase.Configuration
{
    /// <summary>
    /// Represents the "default" ports that come pre-configured with Couchbase Server.
    /// </summary>
    public enum DefaultPorts
    {
        /// <summary>
        /// The Managment REST API port.
        /// </summary>
        MgmtApi = 8091,

        /// <summary>
        /// The Views REST API port.
        /// </summary>
        CApi = 8092,

        /// <summary>
        /// The port used for Binary Memcached TCP operations.
        /// </summary>
        Direct = 11210,

        /// <summary>
        /// Not used by the .NET client - reserved for Moxi.
        /// </summary>
        Proxy = 11211,

        /// <summary>
        /// The SSL port used for Binary Memcached TCP operations.
        /// </summary>
        SslDirect = 11207,

        /// <summary>
        /// The SSL port used by View REST API.
        /// </summary>
        HttpsCApi = 18092,

        /// <summary>
        /// The SSL port used by the Managment REST API's.
        /// </summary>
        HttpsMgmt = 18091
    }
}
