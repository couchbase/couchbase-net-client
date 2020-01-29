using System;
using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates new <see cref="ICollection"/> implementations.
    /// </summary>
    internal interface ICollectionFactory
    {
        /// <summary>
        /// Create a new <see cref="ICollection"/>.
        /// </summary>
        /// <param name="bucket">The owning <seealso cref="BucketBase"/>.</param>
        /// <param name="cid">The collection ID, if any.</param>
        /// <param name="name">The collection name.</param>
        /// <param name="scopeName">The owning scope name.</param>
        /// <returns>The new collection.</returns>
        ICollection Create(BucketBase bucket, uint? cid, string name, string scopeName);
    }
}
