using Couchbase.Diagnostics;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Protostellar.Query.V1;
using Couchbase.Views;
using Couchbase.Stellar.CouchbaseClient.Admin;

namespace Couchbase.Stellar.CouchbaseClient
{
    internal class ProtoBucket : IBucket
    {
        private readonly ProtoCluster _cluster;
        private readonly QueryService.QueryServiceClient _queryClient;
        private readonly ProtoCollectionManager _collectionManager;

        internal ProtoBucket(string name, ProtoCluster cluster, QueryService.QueryServiceClient queryClient)
        {
            Name = name;
            _cluster = cluster;
            _queryClient = queryClient;
            _collectionManager = new ProtoCollectionManager(cluster.GrpcChannel, name);
        }

        public bool SupportsCollections => true;

        public string Name { get; }


        public ICluster Cluster => _cluster;

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

        public IScope Scope(string scopeName) => new ProtoScope(scopeName, this, _cluster, _queryClient);

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
}
