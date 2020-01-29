using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.HTTP;
using Couchbase.Diagnostics;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AnalyticsOptions = Couchbase.Analytics.AnalyticsOptions;

#nullable enable

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private readonly ILogger<Cluster> _logger;
        private readonly IRetryOrchestrator _retryOrchestrator;
        private readonly object _syncObject = new object();
        private bool _disposed;
        private readonly ClusterContext _context;
        private bool _hasBootStrapped;
        private readonly SemaphoreSlim _bootstrapLock = new SemaphoreSlim(1);

        // Internal is used to provide a seam for unit tests
        internal Lazy<IQueryClient> LazyQueryClient;
        internal Lazy<ISearchClient> LazySearchClient;
        internal Lazy<IAnalyticsClient> LazyAnalyticsClient;
        internal Lazy<IUserManager> LazyUserManager;
        internal Lazy<IBucketManager> LazyBucketManager;
        internal Lazy<IQueryIndexManager> LazyQueryManager;
        internal Lazy<ISearchIndexManager> LazySearchManager;

        internal Cluster(string connectionString, ClusterOptions? clusterOptions = null)
        {
            clusterOptions ??= new ClusterOptions();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidConfigurationException("The connectionString cannot be null, empty or only be whitespace.");
            }
            if (clusterOptions == null)
            {
                throw new InvalidConfigurationException("ClusterOptions is null.");
            }
            if (string.IsNullOrWhiteSpace(clusterOptions.Password) || string.IsNullOrWhiteSpace(clusterOptions.UserName))
            {
                throw new InvalidConfigurationException("Username and password are required.");
            }

            clusterOptions.ConnectionString(connectionString);

            var configTokenSource = new CancellationTokenSource();
            _context = new ClusterContext(configTokenSource, clusterOptions);
            _context.StartConfigListening();

            var httpClient = _context.ServiceProvider.GetRequiredService<CouchbaseHttpClient>();
            LazyQueryClient = new Lazy<IQueryClient>(() => new QueryClient(_context));
            LazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => new AnalyticsClient(_context));
            LazySearchClient = new Lazy<ISearchClient>(() => new SearchClient(_context));
            LazyQueryManager = new Lazy<IQueryIndexManager>(() => new QueryIndexManager(LazyQueryClient.Value));
            LazyBucketManager = new Lazy<IBucketManager>(() => new BucketManager(_context));
            LazyUserManager = new Lazy<IUserManager>(() => new UserManager(_context));
            LazySearchManager = new Lazy<ISearchIndexManager>(() => new SearchIndexManager(_context, httpClient));

            _logger = _context.ServiceProvider.GetRequiredService<ILogger<Cluster>>();
            _retryOrchestrator = _context.ServiceProvider.GetRequiredService<IRetryOrchestrator>();
        }

        public static async Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? options = null)
        {
            var cluster = new Cluster(connectionString, options);
            await cluster.InitializeAsync().ConfigureAwait(false);
            return cluster;
        }

        public static Task<ICluster> ConnectAsync(string connectionString, string username, string password)
        {
            return ConnectAsync(connectionString, new ClusterOptions
            {
                UserName = username,
                Password = password
            });
        }

        public static Task<ICluster> ConnectAsync(string connectionString, Action<ConfigurationBuilder> configureBuilder)
        {
            var builder = new ConfigurationBuilder();
            configureBuilder(builder);

            var clusterOptions = builder
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            return ConnectAsync(connectionString, clusterOptions);
        }

        internal async Task InitializeAsync()
        {
            try
            {
                await _context.InitializeAsync();
                _hasBootStrapped = _context.GlobalConfig != null;
                UpdateClusterCapabilities();
            }
            catch (AuthenticationFailureException e)
            {
                //auth failed so bubble up exception and clean up resources
                _logger.LogError(e, @"Could not authenticate user {_clusterOptions.UserName}");

                _context.RemoveNodes();
                throw;
            }
        }

        public async Task<IBucket> BucketAsync(string name)
        {
            var bucket = await _context.GetOrCreateBucketAsync(name);
            _hasBootStrapped = true;
            return bucket;
        }

        public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
        {
            options ??= new DiagnosticsOptions();
            return Task.FromResult(DiagnosticsReportProvider.CreateDiagnosticsReport(_context, options.ReportIdValue ?? Guid.NewGuid().ToString()));
        }

        /// <summary>
        /// Seam for unit tests.
        /// </summary>
        protected internal virtual async Task EnsureBootstrapped()
        {
            if (_hasBootStrapped)
            {
                return;
            }

            // if no buckets registered in cluster, throw exception
            if (!_context.ClusterOptions.Buckets.Any())
            {
                throw new CouchbaseException("Unable to bootstrap - please open a bucket or add a bucket name to the clusterOptions.");
            }

            await _bootstrapLock.WaitAsync();
            try
            {
                // check again to make sure we still need to bootstrap
                if (_hasBootStrapped)
                {
                    return;
                }

                // try to bootstrap first bucket in cluster
                await BucketAsync(_context.ClusterOptions.Buckets.First()).ConfigureAwait(false);
                UpdateClusterCapabilities();
            }
            finally
            {
                _bootstrapLock.Release(1);
            }
        }

        #region Query

        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
        {
            options ??= new QueryOptions();
            await EnsureBootstrapped();

            if (options.CurrentContextId == null)
            {
                options.ClientContextId(Guid.NewGuid().ToString());
            }

            async Task<IQueryResult<T>> Func()
            {
                var client1 = LazyQueryClient.Value;
                var statement1 = statement;
                var options1 = options;
                return await client1.QueryAsync<T>(statement1, options1).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, new QueryRequest
            {
                Options = options,
                Statement = statement,
                Token = options.Token,
                Timeout = options.TimeoutValue
            }).ConfigureAwait(false);
        }

        #endregion

        #region Analytics

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            options ??= new AnalyticsOptions();
            await EnsureBootstrapped();

            var query = new AnalyticsRequest(statement)
            {
                ClientContextId = options.ClientContextIdValue,
                NamedParameters = options.NamedParameters,
                PositionalArguments = options.PositionalParameters,
                Timeout = options.TimeoutValue
            };
            query.Priority(options.PriorityValue);
            query.ScanConsistency(options.ScanConsistencyValue);

            async Task<IAnalyticsResult<T>> Func()
            {
                var client1 = LazyAnalyticsClient.Value;
                var query1 = query;
                var options1 = options ?? new AnalyticsOptions();
                return await client1.QueryAsync<T>(query1, options1.Token).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, query).ConfigureAwait(false);
        }

        #endregion

        #region Search

        public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, ISearchOptions? options = default)
        {
            options ??= new SearchOptions();

            await EnsureBootstrapped();

            var searchRequest = new SearchRequest
            {
                Index = indexName,
                Query = query,
                Options = options,
                Token = ((SearchOptions)options).Token,
                Timeout = ((SearchOptions)options).TimeOut
            };

            async Task<ISearchResult> Func()
            {
                var client1 = LazySearchClient.Value;
                var request1 = searchRequest;
                return await client1.QueryAsync(request1, request1.Token).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, searchRequest).ConfigureAwait(false);
        }

        #endregion

        public IQueryIndexManager QueryIndexes => LazyQueryManager.Value;

        public IAnalyticsIndexManager AnalyticsIndexes => throw new NotImplementedException();

        public ISearchIndexManager SearchIndexes => LazySearchManager.Value;

        public IBucketManager Buckets => LazyBucketManager.Value;

        public IUserManager Users => LazyUserManager.Value;

        public void Dispose()
        {
            if (_disposed) return;
            lock (_syncObject)
            {
                if(_disposed) return;
                _disposed = true;
                _context.Dispose();
            }
        }

        internal void UpdateClusterCapabilities()
        {
            if (LazyQueryClient.Value is QueryClient client)
            {
                if (_context.GlobalConfig != null)
                {
                    client.UpdateClusterCapabilities(_context.GlobalConfig.GetClusterCapabilities());
                }
            }
        }
    }
}
