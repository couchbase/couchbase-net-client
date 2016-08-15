using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Management.Indexes;

namespace Couchbase.Management
{
    /// <summary>
    /// An intermediate class for doing management operations on a Bucket.
    /// </summary>
    public interface IBucketManager : IDisposable
    {
        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult InsertDesignDocument(string designDocName, string designDoc);

        /// <summary>
        /// Inserts a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> InsertDesignDocumentAsync(string designDocName, string designDoc);

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult UpdateDesignDocument(string designDocName, string designDoc);

        /// <summary>
        /// Updates a design document containing a number of views.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <param name="designDoc">A design document JSON string.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> UpdateDesignDocumentAsync(string designDocName, string designDoc);

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        IResult<string> GetDesignDocument(string designDocName);

        /// <summary>
        /// Retrieves the contents of a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A design document object.</returns>
        Task<IResult<string>> GetDesignDocumentAsync(string designDocName);

        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A boolean value indicating the result.</returns>
        IResult RemoveDesignDocument(string designDocName);

        /// <summary>
        /// Removes a design document.
        /// </summary>
        /// <param name="designDocName">The name of the design document.</param>
        /// <returns>A boolean value indicating the result.</returns>
        Task<IResult> RemoveDesignDocumentAsync(string designDocName);

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>The design document as a string.</returns>
        IResult<string> GetDesignDocuments(bool includeDevelopment = false);

        /// <summary>
        /// Lists all existing design documents.
        /// </summary>
        /// <param name="includeDevelopment">Whether or not to show development design documents in the results.</param>
        /// <returns>The design document as a string.</returns>
        Task<IResult<string>> GetDesignDocumentsAsync(bool includeDevelopment = false);

        /// <summary>
        /// Destroys all documents stored within a bucket.  This functionality must also be enabled within the server-side bucket settings for safety reasons.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating success.</returns>
        IResult Flush();

        /// <summary>
        /// Destroys all documents stored within a bucket.  This functionality must also be enabled within the server-side bucket settings for safety reasons.
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating success.</returns>
        Task<IResult> FlushAsync();

        /// <summary>
        /// The name of the Bucket.
        /// </summary>
        string BucketName { get; }

        /// <summary>
        /// Lists the indexes for the current <see cref="IBucketManager"/>.
        /// </summary>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        IndexResult ListN1qlIndexes();

        /// <summary>
        /// Lists the indexes for the current <see cref="IBucketManager"/> asynchronously.
        /// </summary>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        Task<IndexResult> ListN1qlIndexesAsync();

        /// <summary>
        /// Creates the primary index for the current bucket if it doesn't already exist.
        /// </summary>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        // ReSharper disable once InconsistentNaming
        IResult CreateN1qlPrimaryIndex(bool defer);

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket"/> asynchronously.
        /// </summary>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult> CreateN1qlPrimaryIndexAsync(bool defer);

        /// <summary>
        /// Creates a named primary index on the current <see cref="IBucket"/> asynchronously.
        /// </summary>
        /// <param name="customName">The name of the custom index.</param>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult> CreateN1qlPrimaryIndexAsync(string customName, bool defer);

        /// <summary>
        /// Creates a secondary index with optional fields asynchronously.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult> CreateN1qlIndexAsync(string indexName, bool defer, string[] fields);

        /// <summary>
        /// Drops the primary index of the current <see cref="IBucket"/> asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult> DropN1qlPrimaryIndexAsync();

        /// <summary>
        /// Drops the named primary index on the current <see cref="IBucket"/> asynchronously.
        /// </summary>
        /// <param name="customName">Name of the primary index to drop.</param>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        Task<IResult> DropNamedPrimaryIndexAsync(string customName);

        /// <summary>
        /// Drops an index by name asynchronously.
        /// </summary>
        /// <param name="name">The name of the index to drop.</param>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult> DropN1qlIndexAsync(string name);

        /// <summary>
        /// Builds any indexes that have been created with the "defer" flag and are still in the "pending" or "deferred" state asynchronously.
        /// </summary>
        /// <returns>A <see cref="Task{IResult}"/> for awaiting on that contains the result of the method.</returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult[]> BuildN1qlDeferredIndexesAsync();

        /// <summary>
        /// Watches the indexes asynchronously.
        /// </summary>
        /// <param name="watchList">The watch list.</param>
        /// <param name="watchPrimary">if set to <c>true</c> [watch primary].</param>
        /// <param name="watchTimeout">The watch timeout.</param>
        /// <param name="watchTimeUnit">The watch time unit.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        Task<IResult<List<IndexInfo>>> WatchN1qlIndexesAsync(List<string> watchList, bool watchPrimary, long watchTimeout, TimeSpan watchTimeUnit);

        /// <summary>
        /// Creates a primary index on the current <see cref="IBucket"/> reference.
        /// </summary>
        /// <param name="customName">The name of the index.</param>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <returns>An <see cref="IResult"/> with the status of the request.</returns>
        // ReSharper disable once InconsistentNaming
        IResult CreateN1qlPrimaryIndex(string customName, bool defer);

        /// <summary>
        /// Creates a secondary index on the current <see cref="IBucket"/> reference.
        /// </summary>
        /// <param name="indexName">Name of the index to create.</param>
        /// <param name="defer"> If set to <c>true</c>, the N1QL query will use the "with defer" syntax and the index will simply be "pending" (prior to 4.5) or "deferred" (at and after 4.5, see MB-14679).</param>
        /// <param name="fields">The fields to index on.</param>
        /// <returns>An <see cref="IResult"/> with the status of the request.</returns>
        // ReSharper disable once InconsistentNaming
        IResult CreateN1qlIndex(string indexName, bool defer, params string[] fields);

        /// <summary>
        /// Drops the primary index on the current <see cref="IBucket"/>.
        /// </summary>
        /// <returns>An <see cref="IResult"/> with the status of the request.</returns>
        // ReSharper disable once InconsistentNaming
        IResult DropN1qlPrimaryIndex();

        /// <summary>
        /// Drops the named primary index if it exists on the current <see cref="IBucket"/>.
        /// </summary>
        /// <param name="customName">Name of primary index.</param>
        /// <returns>An <see cref="IResult"/> with the status of the request.</returns>
        // ReSharper disable once InconsistentNaming
        IResult DropN1qlPrimaryIndex(string customName);

        /// <summary>
        /// Drops a secondary index on the current <see cref="IBucket"/> reference.
        /// </summary>
        /// <param name="name">The name of the secondary index to drop.</param>
        /// <returns>An <see cref="IResult"/> with the status of the request.</returns>
        // ReSharper disable once InconsistentNaming
        IResult DropN1qlIndex(string name);

        /// <summary>
        /// Builds any indexes that have been created with the "defer" flag and are still in the "pending" state on the current <see cref="IBucket"/>.
        /// </summary>
        /// <returns>An <see cref="IList{IResult}"/> with the status for each index built.</returns>
        // ReSharper disable once InconsistentNaming
        IList<IResult> BuildN1qlDeferredIndexes();

        /// <summary>
        /// Watches the indexes.
        /// </summary>
        /// <param name="watchList">The watch list.</param>
        /// <param name="watchPrimary">if set to <c>true</c> [watch primary].</param>
        /// <param name="watchTimeout">The watch timeout.</param>
        /// <param name="watchTimeUnit">The watch time unit.</param>
        /// <returns></returns>
        // ReSharper disable once InconsistentNaming
        IResult<List<IndexInfo>> WatchN1qlIndexes(List<string> watchList, bool watchPrimary, long watchTimeout, TimeSpan watchTimeUnit);
    }
}