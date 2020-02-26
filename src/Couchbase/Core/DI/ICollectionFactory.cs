using System;
using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates new <see cref="ICouchbaseCollection"/> implementations.
    /// </summary>
    internal interface ICollectionFactory
    {
        /// <summary>
        /// Create a new <see cref="ICouchbaseCollection"/>.
        /// </summary>
        /// <param name="bucket">The owning <seealso cref="BucketBase"/>.</param>
        /// <param name="cid">The collection ID, if any.</param>
        /// <param name="name">The collection name.</param>
        /// <param name="scopeName">The owning scope name.</param>
        /// <returns>The new collection.</returns>
        ICouchbaseCollection Create(BucketBase bucket, uint? cid, string name, string scopeName);
    }
}
