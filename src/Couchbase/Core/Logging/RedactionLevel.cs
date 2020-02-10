namespace Couchbase.Core.Logging
{
    /// <summary>
    /// Specifies the level of log redaction.
    /// </summary>
    public enum RedactionLevel
    {
        /// <summary>
        /// No redaction is performed; this is the default.
        /// </summary>
        None,

        /// <summary>
        /// Only user data is redacted; system and metadata are not.
        /// </summary>
        Partial,

        /// <summary>
        /// User, system, and metadata are redacted.
        /// </summary>
        Full
    }
}
