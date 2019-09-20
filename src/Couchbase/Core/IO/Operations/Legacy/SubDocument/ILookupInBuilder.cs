using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Services.KeyValue;

namespace Couchbase.Core.IO.Operations.Legacy.SubDocument
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
        /// <param name="pathFlags">The path flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Get(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

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
        /// <param name="pathFlags">The lookup flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> Exists(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

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
        /// <param name="pathFlags">The subdocument lookup flags.</param>
        /// <param name="docFlags">The document flags.</param>
        /// <returns>A <see cref="ILookupInBuilder{TDocument}"/> implementation reference for chaining operations.</returns>
        /// <remarks>Requires Couchbase Server 5.0 or higher</remarks>
        ILookupInBuilder<TDocument> GetCount(string path, SubdocPathFlags pathFlags, SubdocDocFlags docFlags = SubdocDocFlags.None);

        /// <summary>
        /// The maximum time allowed for an operation to live before timing out.
        /// </summary>
        /// <param name="timeout">The timeout.</param>
        /// <returns>An <see cref="ILookupInBuilder{TDocument}"/> reference for chaining operations.</returns>
        ILookupInBuilder<TDocument> WithTimeout(TimeSpan timeout);
    }
}
