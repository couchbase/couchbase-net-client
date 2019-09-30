using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with subdocument paths.
    /// </summary>
    [Flags]
    public enum SubdocPathFlags : byte
    {
        /// <summary>
        /// No path flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Creates path if it does not exist.
        /// </summary>
        CreatePath = 0x01,

        /// <summary>
        /// Path refers to a location within the document’s attributes section.
        /// </summary>
        Xattr = 0x04,

        /// <summary>
        /// Indicates that the server should expand any macros before storing the value. Infers <see cref="F:SubdocDocFlags.Xattr"/>.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        ExpandMacroValues = 0x010
    }
}
