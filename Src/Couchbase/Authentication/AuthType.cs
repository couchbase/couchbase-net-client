namespace Couchbase.Authentication
{
    /// <summary>
    /// The type of authentication to use with a given bucket.
    /// </summary>
    public enum AuthType
    {
        /// <summary>
        /// Use no authentication.
        /// </summary>
        None,

        /// <summary>
        /// Use Simple Authentication and Security Layer (SASL) authentication.
        /// </summary>
        Sasl
    }
}
