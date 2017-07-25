namespace Couchbase.Core
{
    /// <summary>
    /// Exposes a "builder" API for constructing a chain of read commands on a document within Couchbase.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    public interface ILookupInBuilder<TDocument> : ISubDocBuilder<TDocument>
    {
        /// <summary>
        /// Gets the value at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Get(string path);

        /// <summary>
        /// Gets the value at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="flags">The subdocument lookup flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Get(string path, SubdocLookupFlags flags);

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Exists(string path);

        /// <summary>
        /// Checks for the existence of a given N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="flags">The subdocument lookup flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Exists(string path, SubdocLookupFlags flags);

        /// <summary>
        /// Gets the number of items in a collection or dictionary at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        /// <remarks>Requires Couchbase Server 5.0 or higher</remarks>
        ILookupInBuilder<TDocument> GetCount(string path);

        /// <summary>
        /// Gets the number of items in a collection or dictionary at a specified N1QL path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="flags">The subdocument lookup flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        /// <remarks>Requires Couchbase Server 5.0 or higher</remarks>
        ILookupInBuilder<TDocument> GetCount(string path, SubdocLookupFlags flags);
    }
}