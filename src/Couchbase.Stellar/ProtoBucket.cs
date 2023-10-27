using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Stellar.KeyValue;
using Couchbase.Stellar.Management.Collections;
using Couchbase.Views;

namespace Couchbase.Stellar;

internal class ProtoBucket : IBucket
{
    private readonly ProtoCluster _protoCluster;
    private readonly QueryService.QueryServiceClient _queryClient;
    private readonly ProtoCollectionManager _collectionManager;

    internal ProtoBucket(string name, ProtoCluster protoCluster, QueryService.QueryServiceClient queryClient)
    {
        Name = name;
        _protoCluster = protoCluster;
        _queryClient = queryClient;
        _collectionManager = new ProtoCollectionManager(_protoCluster, name);
    }

    public bool SupportsCollections => true;

    public string Name { get; }


    public ICluster Cluster => _protoCluster;

    public IViewIndexManager ViewIndexes => throw new NotImplementedException();

    public ICouchbaseCollectionManager Collections => _collectionManager;

    public ICouchbaseCollection Collection(string collectionName)
    {
        throw new NotImplementedException();
    }

    public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
    {
        throw new NotImplementedException();
    }

    public ICouchbaseCollection DefaultCollection()
    {
        throw new NotImplementedException();
    }

    public ValueTask<ICouchbaseCollection> DefaultCollectionAsync()
    {
        throw new NotImplementedException();
    }

    public IScope DefaultScope() => Scope("_default");

    public ValueTask<IScope> DefaultScopeAsync() => ValueTask.FromResult(DefaultScope());

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        throw new NotImplementedException();
    }

    public Task<IPingReport> PingAsync(PingOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public IScope Scope(string scopeName) => new ProtoScope(scopeName, this, _protoCluster, _queryClient);

    public ValueTask<IScope> ScopeAsync(string scopeName) => ValueTask.FromResult(Scope(scopeName));

    public Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = null)
    {
        throw new NotImplementedException();
    }

    public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
    {
        throw new NotImplementedException();
    }
}
