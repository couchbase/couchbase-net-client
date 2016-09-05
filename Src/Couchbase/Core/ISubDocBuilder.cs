using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.IO.SubDocument;
using Couchbase.Core.Serialization;

namespace Couchbase.Core
{
    public interface ISubDocBuilder<TDocument> : ITypeSerializerProvider
    {
        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        IDocumentFragment<TDocument> Execute();

        /// <summary>
        /// Executes the chained operations.
        /// </summary>
        /// <returns>
        /// A <see cref="T:Couchbase.IDocumentFragment`1" /> representing the results of the chained operations.
        /// </returns>
        Task<IDocumentFragment<TDocument>> ExecuteAsync();

        /// <summary>
        /// Gets or sets the unique identifier for the document.
        /// </summary>
        /// <value>
        /// The key.
        /// </value>
        string Key { get; }

        /// <summary>
        /// Returns a count of the currently chained operations.
        /// </summary>
        /// <returns></returns>
        int Count { get; }
    }
}