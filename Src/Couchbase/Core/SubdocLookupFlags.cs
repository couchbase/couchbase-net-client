using System;

namespace Couchbase.Core
{
    /// <summary>
    /// Flags for Subdoc lookup operations.
    /// </summary>
    [Flags]
    public enum SubdocLookupFlags : byte
    {
        /// <summary>
        /// No subdoc flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Path refers to a location within the document’s attributes section.
        /// </summary>
        XattrPath = 0x04,

        /// <summary>
        /// Allows access to a deleted document's attributes section. Infers <see cref="F:SubdocLookupFlags.XattrPath"/>.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        AccessDeleted = 0x08
    }
}