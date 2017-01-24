using System;

namespace Couchbase.Core
{
    /// <summary>
    /// Flags for Subdoc mutate operations.
    /// </summary>
    [Flags]
    public enum SubdocMutateFlags : byte
    {
        /// <summary>
        /// No subdoc flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Creates path if it does not exist.
        /// </summary>
        CreatePath = 0x01,

        /// <summary>
        /// Creates the document if it does not exist.
        /// </summary>
        CreateDocument = 0x02,

        /// <summary>
        /// Path refers to a location within the document’s attributes section.
        /// </summary>
        AttributePath = 0x04,

        /// <summary>
        /// Indicates that the server should expand any macros before storing the value. Infers <see cref="F:SubdocMutateFlags.AttributePath"/>.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        ExpandMacro = 0x010
    }
}
