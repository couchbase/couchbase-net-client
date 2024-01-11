#nullable enable
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Diagnostics;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;

namespace Couchbase.UnitTests.Core.Diagnostics.Tracing.Fakes
{
    public class FakeCluster : ICluster
    {
        private readonly ConcurrentDictionary<string, IBucket> _buckets = new ConcurrentDictionary<string, IBucket>();
        private readonly ClusterOptions _clusterOptions;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public FakeCluster(ClusterOptions clusterOptions)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            _clusterOptions = clusterOptions;
        }

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }

        public IServiceProvider ClusterServices { get; }
        public ValueTask<IBucket> BucketAsync(string name)
        {
            if (_buckets.TryGetValue("default", out IBucket? bucket))
            {
                return new ValueTask<IBucket>(bucket);
            }

            bucket = new FakeBucket("default", _clusterOptions);
            _buckets.TryAdd("default", bucket);
            return new ValueTask<IBucket>(bucket);
        }

        public Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = default)
        {
            throw new NotImplementedException();
        }

        public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            throw new NotImplementedException();
        }

        public Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = default)
        {
            throw new NotImplementedException();
        }

        public Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public IQueryIndexManager QueryIndexes { get; }
        public IAnalyticsIndexManager AnalyticsIndexes { get; }
        public ISearchIndexManager SearchIndexes { get; }
        public IBucketManager Buckets { get; }
        public IUserManager Users { get; }
        public IEventingFunctionManager EventingFunctions { get; }
        public Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, SearchOptions? options)
        {
            throw new NotImplementedException();
        }
    }
}
