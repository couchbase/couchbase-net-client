using Couchbase.Analytics;
using Couchbase.KeyValue;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;

namespace Couchbase.Stellar.KeyValue;

internal class ProtoScope : IScope
{
    public const string DefaultScopeName = "_default";

    public ProtoScope(string name, ProtoBucket protoBucket, ProtoCluster protoCluster, QueryService.QueryServiceClient queryClient)
    {
        Name = name;
        _protoBucket = protoBucket;
        _protoCluster = protoCluster;
        _queryClient = queryClient;
        IsDefaultScope = (Name == DefaultScopeName);
    }

    public ICouchbaseCollection this[string name] => throw new NotImplementedException();

    public string Name { get; }

    private readonly ProtoBucket _protoBucket;
    private readonly ProtoCluster _protoCluster;
    private readonly QueryService.QueryServiceClient _queryClient;

    public IBucket Bucket => _protoBucket;

    public bool IsDefaultScope { get; }

    public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = null)
    {
        return _protoCluster.AnalyticsQueryAsync<T>(statement, _protoBucket.Name, Name, options);
    }

    public ICouchbaseCollection Collection(string collectionName) => new ProtoCollection(collectionName, this, _protoCluster);

    public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName) =>
        ValueTask.FromResult(Collection(collectionName));

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;
        opts = opts with
        {
            ScopeName = Name,
            BucketName = Bucket.Name,
        };

        return await _protoCluster.QueryAsync<T>(statement, opts).ConfigureAwait(false);
    }
}
