using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Logging;
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
        private readonly IRedactor _redactor;

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

            LazyQueryClient = new Lazy<IQueryClient>(() => _context.ServiceProvider.GetRequiredService<IQueryClient>());
            LazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => _context.ServiceProvider.GetRequiredService<IAnalyticsClient>());
            LazySearchClient = new Lazy<ISearchClient>(() => _context.ServiceProvider.GetRequiredService<ISearchClient>());
            LazyQueryManager = new Lazy<IQueryIndexManager>(() => _context.ServiceProvider.GetRequiredService<IQueryIndexManager>());
            LazyBucketManager = new Lazy<IBucketManager>(() => _context.ServiceProvider.GetRequiredService<IBucketManager>());
            LazyUserManager = new Lazy<IUserManager>(() => _context.ServiceProvider.GetRequiredService<IUserManager>());
            LazySearchManager = new Lazy<ISearchIndexManager>(() => _context.ServiceProvider.GetRequiredService<ISearchIndexManager>());

            _logger = _context.ServiceProvider.GetRequiredService<ILogger<Cluster>>();
            _retryOrchestrator = _context.ServiceProvider.GetRequiredService<IRetryOrchestrator>();
            _redactor = _context.ServiceProvider.GetRequiredService<IRedactor>();
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
                _logger.LogError(e,
                    "Could not authenticate user {username}", _redactor.UserData(_context.ClusterOptions.UserName ?? string.Empty));

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

        public async Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
        {
            options ??= new DiagnosticsOptions();
            return await DiagnosticsReportProvider.CreateDiagnosticsReportAsync(_context, options.ReportIdValue ?? Guid.NewGuid().ToString())
                .ConfigureAwait(false);
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
                var bucketName = _context.ClusterOptions.Buckets.FirstOrDefault();
                _logger.LogDebug("Attempting to bootstrap bucket {bucketname}", _redactor.MetaData(bucketName));
                await BucketAsync(bucketName).ConfigureAwait(false);
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
            options.TimeoutValue ??= _context.ClusterOptions.QueryTimeout;

            await EnsureBootstrapped();

            async Task<IQueryResult<T>> Func()
            {
                var client1 = LazyQueryClient.Value;
                var statement1 = statement;
                var options1 = options!;
                return await client1.QueryAsync<T>(statement1, options1).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, new QueryRequest
            {
                Options = options,
                Statement = statement,
                Token = options.Token,
                Timeout = options.TimeoutValue.Value
            }).ConfigureAwait(false);
        }

        #endregion

        #region Analytics

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            options ??= new AnalyticsOptions();
            options.TimeoutValue ??= _context.ClusterOptions.AnalyticsTimeout;

            await EnsureBootstrapped();

            var query = new AnalyticsRequest(statement)
            {
                ClientContextId = options.ClientContextIdValue,
                NamedParameters = options.NamedParameters,
                PositionalArguments = options.PositionalParameters,
                Timeout = options.TimeoutValue.Value
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

        public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = default)
        {
            options ??= new SearchOptions();
            options.TimeoutValue ??= _context.ClusterOptions.SearchTimeout;

            await EnsureBootstrapped();

            var searchRequest = new SearchRequest
            {
                Index = indexName,
                Query = query,
                Options = options,
                Token = options.Token,
                Timeout = options.TimeoutValue.Value
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
