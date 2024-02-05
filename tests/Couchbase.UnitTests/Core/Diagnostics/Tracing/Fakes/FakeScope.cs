using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.KeyValue;
using Couchbase.Management.Search;
using Couchbase.Query;
using Couchbase.Search;

#pragma warning disable CS8632
namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
    public class FakeScope : IScope
    {
        private readonly ClusterOptions _clusterOptions;

        private readonly ConcurrentDictionary<string, ICouchbaseCollection> _collections =
            new ConcurrentDictionary<string, ICouchbaseCollection>();

        public FakeScope(string name, IBucket bucket, ClusterOptions clusterOptions)
        {
            _clusterOptions = clusterOptions;
            Name = name;
            Bucket = bucket;
        }

        public string Id { get; }
        public string Name { get; }
        public IBucket Bucket { get; }
        public bool IsDefaultScope => Name == "_default";

        public ICouchbaseCollection this[string name] => throw new NotImplementedException();

        public ICouchbaseCollection Collection(string collectionName)
        {
            throw new NotImplementedException();
        }

        public ValueTask<ICouchbaseCollection> CollectionAsync(string collectionName)
        {
            if (_collections.TryGetValue(collectionName, out ICouchbaseCollection collection))
            {
                return new ValueTask<ICouchbaseCollection>(collection);
            }

            collection = new FakeCollection(collectionName, this, Bucket, _clusterOptions);
            _collections.TryAdd(collectionName, collection);
            return new ValueTask<ICouchbaseCollection>(collection);
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = default)
        {
            throw new NotImplementedException();
        }

        public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            throw new NotImplementedException();
        }

        public ISearchIndexManager SearchIndexes => throw new NotImplementedException();

        public Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, SearchOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
