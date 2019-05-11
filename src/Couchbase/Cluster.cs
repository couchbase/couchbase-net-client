using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Logging;
using Couchbase.Management;
using Couchbase.Services.Analytics;
using Couchbase.Services.Query;
using Couchbase.Services.Search;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private readonly ConcurrentDictionary<string, IBucket> _bucketRefs = new ConcurrentDictionary<string, IBucket>();
        private static readonly ILogger Log = LogManager.CreateLogger<Cluster>();
        private bool _disposed;
        private IConfiguration _configuration;
        private IQueryClient _queryClient;
        private ISearchClient _searchClient;
        private IAnalyticsClient _analyticsClient;

        public Cluster(IConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new InvalidConfigurationException("Configuration is null.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Password) || string.IsNullOrWhiteSpace(configuration.UserName))
            {
                throw new InvalidConfigurationException("Username and password are required.");
            }

            _configuration = configuration;

            Connect();
        }

        public Cluster(string connectionStr, string username, string password)
        {
            var connectionString = ConnectionString.Parse(connectionStr);

            _configuration = new Configuration()
                .WithServers(connectionString.Hosts.ToArray())
                .WithCredentials(username, password);

            // TODO: load connection string params into configuration

            Connect();
        }

        private void Connect()
        {
            if (!_configuration.Buckets.Any())
            {
                _configuration = _configuration.WithBucket("default");
            }

            if (!_configuration.Servers.Any())
            {
                _configuration = _configuration.WithServers("couchbase://localhost");
            }

            Task.Run(async () =>
                {
                    foreach (var configBucket in _configuration.Buckets)
                    {
                        Log.LogDebug("Creating bucket {0}", configBucket);

                        foreach (var configServer in _configuration.Servers)
                        {
                            Log.LogDebug("Bootstrapping bucket {0} using server {1}", configBucket, configServer);

                            var bucket = new CouchbaseBucket(this, configBucket);
                            await bucket.BootstrapAsync(configServer, _configuration).ConfigureAwait(false);
                            _bucketRefs.TryAdd(configBucket, bucket);

                            Log.LogDebug("Succesfully bootstrapped bucket {0} using server {1}", configBucket,
                                configServer);
                            return;
                        }
                    }
                })
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();
        }

        public Task<IBucket> Bucket(string name)
        {
            if (_bucketRefs.TryGetValue(name, out var bucket))
            {
                return Task.FromResult(bucket);
            }

            throw new ArgumentOutOfRangeException(nameof(name), "Bucket not found!");
        }

        public Task<IDiagnosticsReport> Diagnostics(string reportId)
        {
            throw new NotImplementedException();
        }

        public Task<IClusterManager> ClusterManager()
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryParameter parameters = null, IQueryOptions options = null)
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

        #region Analytics

        public Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions options = default)
        {
            if (options == default)
            {
                options = new AnalyticsOptions();
            }

            var query = new AnalyticsRequest(statement);
            query.ClientContextId(options.ClientContextId);
            query.Pretty(options.Pretty);
            query.IncludeMetrics(options.IncludeMetrics);
            query.NamedParameters = options.NamedParameters;
            query.PositionalArguments = options.PositionalParameters;

            if (options.Timeout.HasValue)
            {
                query.Timeout(options.Timeout.Value);
            }

            query.Priority(options.Priority);
            query.Deferred(options.Deferred);

            query.ConfigureLifespan(30); //TODO: use configuration.AnalyticsTimeout

            if (_analyticsClient == null)
            {
                _analyticsClient = new AnalyticsClient(_configuration);
            }
            return _analyticsClient.QueryAsync<T>(query, options.CancellationToken);
        }

        #endregion

        #region Search

        public Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default)
        {
            if (_searchClient == null)
            {
                _searchClient = new SearchClient(_configuration);
            }

            query.Index = indexName;

            if (options == default)
            {
                options = new SearchOptions();
            }
            //TODO: convert options to params

            return _searchClient.QueryAsync(query);
        }

        #endregion

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
