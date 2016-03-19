using System;
using Couchbase.Core.Serialization;

namespace Couchbase.Core
{
    /// <summary>
    /// Exposes a "builder" API for constructing a chain of read commands on a document within Couchbase.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    public interface ILookupInBuilder<out TDocument> : ISubDocBuilder<TDocument>
    {
        /// <summary>
        /// Gets the value at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        ILookupInBuilder<TDocument> Get(string path);

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Exists(string path);
    }
}