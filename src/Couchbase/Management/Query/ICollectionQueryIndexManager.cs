using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;

namespace Couchbase.Management.Query;

/// <summary>
/// This interface contains the means for managing collection-level indexes used for queries.
/// </summary>
public interface ICollectionQueryIndexManager
{
    /// <summary>
    /// Fetches all indexes from the server for the given bucket (limiting to scope/collection if applicable).
    /// </summary>
    /// <param name="options">The operational to specify.</param>
    /// <returns>A <see cref="IEnumerable{T}"/> with the results of the query.</returns>
    Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(GetAllQueryIndexOptions options);

    /// <summary>
    /// Creates a new index.
    /// </summary>
    /// <param name="indexName">The name of the index.</param>
    /// <param name="fields">The fields to create the index over.</param>
    /// <param name="options">Any optional parameters.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task CreateIndexAsync(string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options);

    /// <summary>
    /// Creates a new primary index on the bucket in scope.
    /// </summary>
    /// <param name="options">Any optional fields.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task CreatePrimaryIndexAsync(CreatePrimaryQueryIndexOptions options);

    /// <summary>
    /// Drops an index.
    /// </summary>
    /// <param name="indexName">The name of the index to drop.</param>
    /// <param name="options">Any optional parameters.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task DropIndexAsync(string indexName, DropQueryIndexOptions options);

    /// <summary>
    /// Drops a primary index.
    /// </summary>
    /// <param name="options">Any optional parameters.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task DropPrimaryIndexAsync(DropPrimaryQueryIndexOptions options);

    /// <summary>
    /// An internal collection reference for the query_context.
    /// </summary>
    /// <param name="indexNames">The names of the indexes to watch.</param>
    /// <param name="duration">The time allowed for the operation to be terminated.</param>
    /// <param name="options">Any optional parameters.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task WatchIndexesAsync(IEnumerable<string> indexNames, TimeSpan duration, WatchQueryIndexOptions options);

    /// <summary>
    /// Build Deferred builds all indexes which are currently in deferred state.
    /// </summary>
    /// <param name="options">Any optional parameters.</param>
    /// <returns>A <see cref="Task"/> for awaiting.</returns>
    Task BuildDeferredIndexesAsync(BuildDeferredQueryIndexOptions options);

    /// <summary>
    /// An internal collection reference for the query_context.
    /// </summary>
    internal ICouchbaseCollection Collection { get; set; }

    /// <summary>
    /// An internal scope reference for the query_context.
    /// </summary>
    internal IBucket Bucket { get; set; }
}
