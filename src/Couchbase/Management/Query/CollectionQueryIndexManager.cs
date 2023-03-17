using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Couchbase.KeyValue;
using Couchbase.Utils;

#pragma warning disable CS0618

namespace Couchbase.Management.Query;

/// <inheritdoc />
internal class CollectionQueryIndexManager : ICollectionQueryIndexManager
{
    private readonly IQueryIndexManager _queryIndexManager;
    private ICouchbaseCollection _collection;
    private IBucket _bucket;

    public CollectionQueryIndexManager(IQueryIndexManager queryIndexManager)
    {
        _queryIndexManager = queryIndexManager ?? throw new ArgumentNullException(nameof(queryIndexManager));
    }

    #region Methods

    /// <inheritdoc />
    public Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(GetAllQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.GetAllIndexesAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task CreateIndexAsync(string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.CreateIndexAsync(_bucket.Name, indexName, fields, options);
    }

    /// <inheritdoc />
    public Task CreatePrimaryIndexAsync(CreatePrimaryQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.CreatePrimaryIndexAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task DropIndexAsync(string indexName, DropQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.DropIndexAsync(_bucket.Name, indexName, options);
    }

    /// <inheritdoc />
    public Task DropPrimaryIndexAsync(DropPrimaryQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.DropPrimaryIndexAsync(_bucket.Name, options);
    }

    /// <inheritdoc />
    public Task WatchIndexesAsync(IEnumerable<string> indexNames, TimeSpan duration, WatchQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.WatchIndexesAsync(_bucket.Name, indexNames, options);
    }

    /// <inheritdoc />
    public Task BuildDeferredIndexesAsync(BuildDeferredQueryIndexOptions options)
    {
        options.ScopeName(_collection.Scope.Name);
        options.CollectionName(_collection.Name);
        options.QueryContext = QueryContext.CreateOrDefault(_bucket.Name, _collection.Scope.Name);
        return _queryIndexManager.BuildDeferredIndexesAsync(_bucket.Name, options);
    }

    #endregion

    #region Dependencies

    /// <inheritdoc />
    ICouchbaseCollection ICollectionQueryIndexManager.Collection
    {
        get => _collection;
        set => _collection = value;
    }

    /// <inheritdoc />
    IBucket ICollectionQueryIndexManager.Bucket
    {
        get => _bucket;
        set => _bucket = value;
    }

    #endregion
}
