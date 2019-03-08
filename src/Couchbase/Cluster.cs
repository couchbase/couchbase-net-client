using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Management;
using Couchbase.Services.Analytics;
using Couchbase.Services.Query;
using Couchbase.Services.Search;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private readonly ConcurrentDictionary<string, IBucket> _bucketRefs = new ConcurrentDictionary<string, IBucket>();
        private bool _disposed;
        private IConfiguration _configuration;
        private IQueryClient _queryClient;

        public Cluster(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (string.IsNullOrWhiteSpace(configuration.Password) || string.IsNullOrWhiteSpace(configuration.UserName))
            {
                throw new ArgumentNullException(nameof(configuration), "Username and password are required.");
            }

            _configuration = configuration;

            if (!_configuration.Buckets.Any())
            {
                _configuration = _configuration.WithBucket("default");
            }

            if (!_configuration.Servers.Any())
            {
                _configuration = _configuration.WithServers("couchbase://localhost");
            }

            var task = Task.Run(async () =>
            {
                foreach (var configBucket in _configuration.Buckets)
                {
                    foreach (var configServer in _configuration.Servers)
                    {
                        var bucket = new CouchbaseBucket(this, configBucket);
                        await bucket.BootstrapAsync(configServer, _configuration).ConfigureAwait(false);
                        _bucketRefs.TryAdd(configBucket, bucket);
                        return;
                    }
                }
            });
            task.ConfigureAwait(false);
            task.Wait();
        }

        public Task<IBucket> Bucket(string name)
        {
            if (_bucketRefs.TryGetValue(name, out var bucket))
            {
                return Task.FromResult(bucket);
            }

            throw new ArgumentOutOfRangeException(nameof(name), "Bucket not found!");
        }

        public Task<IDiagnosticsReport> Diagnostics()
        {
            throw new NotImplementedException();
        }

        public Task<IDiagnosticsReport> Diagnostics(string reportId)
        {
            throw new NotImplementedException();
        }

        public Task<IClusterManager> ClusterManager()
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> Query<T>(string statement, QueryParameter parameters = null, IQueryOptions options = null)
        {
            if (_queryClient == null)
            {
                _queryClient = new QueryClient(_configuration);
            }

            //re-use older API by mapping parameters to new API
            options?.AddNamedParameter(parameters?.NamedParameters.ToArray());
            options?.AddPositionalParameter(parameters?.PostionalParameters.ToArray());

            return _queryClient.QueryAsync<T>(statement, options);
        }

        public Task<IQueryResult<T>> Query<T>(string statement, Action<QueryParameter> parameters = null, Action<IQueryOptions> options = null)
        {
            var queryParameters = new QueryParameter();
            parameters?.Invoke(queryParameters);

            var queryOptions = new QueryOptions();
            options?.Invoke(queryOptions);

            return Query<T>(statement, queryParameters, queryOptions);
        }

        public Task<IAnalyticsResult> AnalyticsQuery<T>(string statement, IAnalyticsOptions options)
        {
            throw new NotImplementedException();
        }

        public Task<ISearchResult> SearchQuery<T>(ISearchQuery query, ISearchOptions options)
        {
            throw new NotImplementedException();
        }

        public IQueryIndexes QueryIndexes { get; }
        public IAnalyticsIndexes AnalyticsIndexes { get; }
        public ISearchIndexes SearchIndexes { get; }
        public IBucketManager Buckets { get; }
        public IUserManager Users { get; }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var bucket in _bucketRefs.Values)
            {
                // maybe this should be an internal Close instead of Dispose to prevent external calls
                bucket.Dispose();
            }

            if (_queryClient is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _disposed = true;
        }
    }
}
