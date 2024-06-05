using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Utils;

#pragma warning disable CS0618
#nullable enable

namespace Couchbase.Management.Query;

/// <inheritdoc />
internal class CollectionQueryIndexManager : ICollectionQueryIndexManager
{
    private readonly IQueryIndexManager _queryIndexManager;
    private readonly IBucket _bucket;
    private readonly ICouchbaseCollection _collection;

    public CollectionQueryIndexManager(IQueryIndexManager queryIndexManager, IBucket bucket, ICouchbaseCollection collection)
    {
        _queryIndexManager = queryIndexManager ?? throw new ArgumentNullException(nameof(queryIndexManager));
        _bucket = bucket ?? throw new ArgumentNullException(nameof(bucket));
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    #region Methods

    /// <inheritdoc />
    public Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(GetAllQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.GetAllIndexesAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task CreateIndexAsync(string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.CreateIndexAsync(_bucket.Name, indexName, fields, options);
    }

    /// <inheritdoc />
    public Task CreatePrimaryIndexAsync(CreatePrimaryQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.CreatePrimaryIndexAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task DropIndexAsync(string indexName, DropQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.DropIndexAsync(_bucket.Name, indexName, options);
    }

    /// <inheritdoc />
    public Task DropPrimaryIndexAsync(DropPrimaryQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.DropPrimaryIndexAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task WatchIndexesAsync(IEnumerable<string> indexNames, TimeSpan duration, WatchQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.WatchIndexesAsync(_bucket.Name, indexNames, options);
    }

    /// <inheritdoc />
    public Task BuildDeferredIndexesAsync(BuildDeferredQueryIndexOptions options)
    {
        IsUsingDeprecatedOptions(options.ScopeNameValue, options.CollectionNameValue);
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.BuildDeferredIndexesAsync(_bucket.Name, options);
    }

    private static void IsUsingDeprecatedOptions(string? scopeName, string? collectionName)
    {
        if (scopeName != null || collectionName != null)
        {
            ThrowHelper.ThrowInvalidArgumentException("Using ScopeName and CollectionName in the options is deprecated. " +
                                                      "The CollectionQueryIndexManager automatically encodes the valid Scope and Collection in the request.");
        }
    }
    #endregion
}
