using Couchbase.Core.IO.Operations;
using System;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Flags for indicating additional actions when working with MutateIn Sub-Document operations.
    /// </summary>
    [Flags]
    public enum StoreSemantics : byte
    {
        /// <summary>
        /// Replace the document; fail if the document does not exist.
        /// </summary>
        Replace = 0x00,

        /// <summary>
        /// Creates the document; update the document if it exists.
        /// </summary>
        Upsert = OpCode.Set,

        /// <summary>
        /// Create the document; fail if it exists.
        /// </summary>
        Insert = OpCode.Add,

        /// <summary>
        /// Allows access to a deleted document's attributes section.
        /// Only for internal diagnostic use only and is an unsupported feature.
        /// </summary>
        AccessDeleted = OpCode.Delete
    }
}
