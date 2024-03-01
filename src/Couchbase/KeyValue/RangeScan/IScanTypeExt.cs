using Couchbase.Core;
using System;

namespace Couchbase.KeyValue.RangeScan
{
    #nullable enable

    /// <summary>
    /// Extends the <see cref="IScanType"/> with additional collection data and serialization.
    /// </summary>
    internal interface IScanTypeExt
    {
        /// <summary>
        /// The name of the collection being scanned; if omitted the scan will be on the default collection.
        /// </summary>
        string? CollectionName { get; set; }

        /// <summary>
        /// Converts the instance into a JSON <see cref="byte"/> array.
        /// </summary>
        /// <returns>A JSON <see cref="byte"/> that represents the <see cref="IScanType"/>.</returns>
        byte[] Serialize(bool keyOnly, TimeSpan timeout, MutationToken? token);

        /// <summary>
        /// Gets a value indicating whether the operation is a sampling scan.  Affects error handling.
        /// </summary>
        bool IsSampling { get; }
    }
}
