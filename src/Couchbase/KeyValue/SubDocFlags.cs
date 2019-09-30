using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with subdocument documents.
    /// </summary>
    [Flags]
    public enum SubdocDocFlags : byte
    {
        /// <summary>
        /// No document flags have been specified.
        /// </summary>
        None = 0x00,

        /// <summary>
        /// Creates the document if it does not exist.
        /// </summary>
        UpsertDocument = 0x01,

        /// <summary>
        /// Similar to <see cref="UpsertDocument"/>, except that the operation only succeds if the document does not exist.
        /// This option makes sense in the context of wishing to create a new document together with Xattrs.
        /// </summary>
        InsertDocument = 0x02,

        /// <summary>
        /// Allows access to a deleted document's attributes section.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        AccessDeleted = 0x04
    }
}
