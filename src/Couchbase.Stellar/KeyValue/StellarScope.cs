using Couchbase.Analytics;
using Couchbase.KeyValue;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Query;
using Couchbase.Stellar.Util;

namespace Couchbase.Stellar.KeyValue;

internal class StellarScope : IScope
{
    public const string DefaultScopeName = "_default";

    public StellarScope(string name, StellarBucket stellarBucket, StellarCluster stellarCluster, QueryService.QueryServiceClient queryClient)
    {
        Name = name;
        _stellarBucket = stellarBucket;
        _stellarCluster = stellarCluster;
        _queryClient = queryClient;
        IsDefaultScope = Name == DefaultScopeName;
    }

    public ICouchbaseCollection this[string name] => throw new UnsupportedInProtostellarException("Cached Collections");

    public string Name { get; }

    private readonly StellarBucket _stellarBucket;
    private readonly StellarCluster _stellarCluster;
    private readonly QueryService.QueryServiceClient _queryClient;

    public IBucket Bucket => _stellarBucket;

    public bool IsDefaultScope { get; }

    public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = null)
    {
        return _stellarCluster.AnalyticsQueryAsync<T>(statement, _stellarBucket.Name, Name, options);
    }

    public ICouchbaseCollection Collection(string collectionName) => new StellarCollection(collectionName, this, _stellarCluster);

    public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName) => ValueTask.FromResult(Collection(collectionName));

    public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
    {
        var opts = options?.AsReadOnly() ?? QueryOptions.DefaultReadOnly;
        opts = opts with
        {
            ScopeName = Name,
            BucketName = Bucket.Name,
        };

        return await _stellarCluster.QueryAsync<T>(statement, opts).ConfigureAwait(false);
    }
}
