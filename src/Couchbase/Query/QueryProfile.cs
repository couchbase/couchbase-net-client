namespace Couchbase.Query
{
    /// <summary>
    /// Query profile information to be returned with the query results.
    /// </summary>
    public enum QueryProfile
    {
        /// <summary>
        /// Returns no query profile information. This is the default.
        /// </summary>
        Off,

        /// <summary>
        /// Returns phase information.
        /// </summary>
        Phases,

        /// <summary>
        /// Returns timing information.
        /// </summary>
        Timings
    }
}
