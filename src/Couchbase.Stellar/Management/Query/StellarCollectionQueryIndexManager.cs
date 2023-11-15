using Couchbase.Management.Query;
#pragma warning disable CS0618
namespace Couchbase.Stellar.Management.Query;
internal class StellarCollectionQueryIndexManager : ICollectionQueryIndexManager
{
    private readonly IQueryIndexManager _queryIndexManager;
    private readonly string _collectionName;
    private readonly string _scopeName;
    private readonly string _bucketName;

    public StellarCollectionQueryIndexManager(IQueryIndexManager queryIndexManager, string bucketName, string scopeName, string collectionName)
    {
        _queryIndexManager = queryIndexManager;
        _bucketName = bucketName;
        _scopeName = scopeName;
        _collectionName = collectionName;
    }
    public async Task<IEnumerable<QueryIndex>> GetAllIndexesAsync(GetAllQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        return await _queryIndexManager.GetAllIndexesAsync(_bucketName, options).ConfigureAwait(false);
    }

    public async Task CreateIndexAsync(string indexName, IEnumerable<string> fields, CreateQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.CreateIndexAsync(_bucketName, indexName, fields, options).ConfigureAwait(false);
    }

    public async Task CreatePrimaryIndexAsync(CreatePrimaryQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.CreatePrimaryIndexAsync(_bucketName, options).ConfigureAwait(false);
    }

    public async Task DropIndexAsync(string indexName, DropQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.DropIndexAsync(_bucketName, indexName, options).ConfigureAwait(false);
    }

    public async Task DropPrimaryIndexAsync(DropPrimaryQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.DropPrimaryIndexAsync(_bucketName, options).ConfigureAwait(false);
    }

    public async Task WatchIndexesAsync(IEnumerable<string> indexNames, TimeSpan duration, WatchQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.WatchIndexesAsync(_bucketName, indexNames, options).ConfigureAwait(false);
    }

    public async Task BuildDeferredIndexesAsync(BuildDeferredQueryIndexOptions options)
    {
        options.ScopeName(_scopeName);
        options.CollectionName(_collectionName);
        await _queryIndexManager.BuildDeferredIndexesAsync(_bucketName, options).ConfigureAwait(false);
    }
}
#pragma warning restore CS0618
