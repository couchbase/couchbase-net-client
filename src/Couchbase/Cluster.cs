using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
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
using Couchbase.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using AnalyticsOptions = Couchbase.Analytics.AnalyticsOptions;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private static readonly ILogger Log = LogManager.CreateLogger<Cluster>();
        private readonly object _syncObject = new object();
        private bool _disposed;
        private readonly ClusterContext _context;
        private bool _hasBootStrapped;
        private readonly SemaphoreSlim _bootstrapLock = new SemaphoreSlim(1);

        private readonly Lazy<IQueryClient> _lazyQueryClient;
        private readonly Lazy<ISearchClient> _lazySearchClient;
        private readonly Lazy<IAnalyticsClient> _lazyAnalyticsClient;
        private readonly Lazy<IUserManager> _lazyUserManager;
        private readonly Lazy<IBucketManager> _lazyBucketManager;
        private readonly Lazy<IQueryIndexManager> _lazyQueryManager;
        private readonly Lazy<ISearchIndexManager> _lazySearchManager;

        internal Cluster(string connectionString, ClusterOptions clusterOptions = null)
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

            clusterOptions.WithConnectionString(connectionString);

            var configTokenSource = new CancellationTokenSource();
            _context = new ClusterContext(configTokenSource, clusterOptions);
            _context.StartConfigListening();

            _lazyQueryClient = new Lazy<IQueryClient>(() => new QueryClient(_context));
            _lazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => new AnalyticsClient(_context));
            _lazySearchClient = new Lazy<ISearchClient>(() => new SearchClient(_context));
            _lazyQueryManager = new Lazy<IQueryIndexManager>(() => new QueryIndexManager(_lazyQueryClient.Value));
            _lazyBucketManager = new Lazy<IBucketManager>(() => new BucketManager(_context));
            _lazyUserManager = new Lazy<IUserManager>(() => new UserManager(_context));
            _lazySearchManager = new Lazy<ISearchIndexManager>(() => new SearchIndexManager(_context));
        }

        public static ICluster Connect(string connectionString, ClusterOptions options = null)
        {
            using (new SynchronizationContextExclusion())
            {
                var cluster = new Cluster(connectionString, options);
                cluster.InitializeAsync().GetAwaiter().GetResult();
                return cluster;
            }
        }

        public static ICluster Connect(string connectionString, string username, string password)
        {
            return Connect(connectionString, new ClusterOptions
            {
                UserName = username,
                Password = password
            });
        }

        public static ICluster Connect(string connectionString, Action<ConfigurationBuilder> configureBuilder)
        {
            var builder = new ConfigurationBuilder();
            configureBuilder(builder);

            var clusterOptions = builder
                .Build()
                .GetSection("couchbase")
                .Get<ClusterOptions>();

            return Connect(connectionString, clusterOptions);
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
                Log.LogError(e, @"Could not authenticate user {_clusterOptions.UserName}");

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

        public Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions options = null)
        {
            options ??= new DiagnosticsOptions();
            return Task.FromResult(DiagnosticsReportProvider.CreateDiagnosticsReport(_context, options?.ReportId ?? Guid.NewGuid().ToString()));
        }

        private async Task EnsureBootstrapped()
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
                await BucketAsync(_context.ClusterOptions.Buckets.First()).ConfigureAwait(false); ;
                UpdateClusterCapabilities();
            }
            finally
            {
                _bootstrapLock.Release(1);
            }
        }

        #region Query

        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options = null)
        {
            options ??= new QueryOptions();
            await EnsureBootstrapped();

            if (options.CurrentContextId == null)
            {
                options.ClientContextId(Guid.NewGuid().ToString());
            }

            async Task<IServiceResult> Func()
            {
                var client1 = _lazyQueryClient.Value;
                var statement1 = statement;
                var options1 = options;
                return await client1.QueryAsync<dynamic>(statement1, options1).ConfigureAwait(false);
            }

            return (IQueryResult<T>)await RetryOrchestrator.RetryAsync(Func, new QueryRequest
            {
                Options = options,
                Statement = statement,
                Token = options.Token,
                Timeout = options.TimeoutValue
            }).ConfigureAwait(false); ;
        }

        #endregion

        #region Analytics

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions options = default)
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

            async Task<IServiceResult> Func()
            {
                var client1 = _lazyAnalyticsClient.Value;
                var query1 = query;
                var options1 = options;
                return await client1.QueryAsync<T>(query1, options1.Token).ConfigureAwait(false);
            }

            return (IAnalyticsResult<T>) await RetryOrchestrator.RetryAsync(Func, query).ConfigureAwait(false); ;
        }

        #endregion

        #region Search

        public async Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default)
        {
            options ??= new SearchOptions();

            await EnsureBootstrapped();

            query.Index = indexName;

            var searchRequest = new SearchRequest
            {
                Query = query,
                Options = options,
                Token = ((SearchOptions)options).Token,
                Timeout = ((SearchOptions)options).TimeOut
            };

            //TODO: convert options to params
            async Task<IServiceResult> Func()
            {
                var client1 = _lazySearchClient.Value;
                var request1 = searchRequest;
                return await client1.QueryAsync(request1.Query, request1.Token).ConfigureAwait(false);
            }

            return (ISearchResult) await RetryOrchestrator.RetryAsync(Func, searchRequest).ConfigureAwait(false);
        }

        #endregion

        public IQueryIndexManager QueryIndexes => _lazyQueryManager.Value;

        public IAnalyticsIndexManager AnalyticsIndexes { get; }

        public ISearchIndexManager SearchIndexes => _lazySearchManager.Value;

        public IBucketManager Buckets => _lazyBucketManager.Value;

        public IUserManager Users => _lazyUserManager.Value;

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
            if (_lazyQueryClient.Value is QueryClient client)
            {
                if (_context.GlobalConfig != null)
                {
                    client.UpdateClusterCapabilities(_context.GlobalConfig.GetClusterCapabilities());
                }
            }
        }
    }
}
