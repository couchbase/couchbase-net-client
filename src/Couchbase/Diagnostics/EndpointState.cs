namespace Couchbase.Diagnostics
{
    public enum EndpointState
    {
        /// <summary>
        /// The endpoint socket is not reachable.
        /// </summary>
        Disconnected,

        /// <summary>
        /// Currently connecting - including auth, etc.
        /// </summary>
        Connecting,

        /// <summary>
        /// Connected and ready.
        /// </summary>
        Connected,

        /// <summary>
        /// Disconnected after being connected.
        /// </summary>
        Disconnecting
    }
}
