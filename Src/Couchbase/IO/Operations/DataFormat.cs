namespace Couchbase.IO.Operations
{
    /// <summary>
    /// Specifies the formatting of data across all SDKs
    /// </summary>
    public enum DataFormat : byte
    {
        /// <summary>
        /// Reserved bit position to avoid zeroing out upper 8 bits
        /// </summary>
        Reserved = 0,

        /// <summary>
        /// Used for SDK specific encodings
        /// </summary>
        Private  = 1,

        /// <summary>
        /// Encode as Json
        /// </summary>
        Json = 2,

        /// <summary>
        /// Store as raw binary format
        /// </summary>
        Binary = 3,

        /// <summary>
        /// Store as a UTF8 string
        /// </summary>
        String = 4
    }
}
