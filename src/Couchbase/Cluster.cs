using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Logging;
using Couchbase.Management;
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

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private static readonly ILogger Log = LogManager.CreateLogger<Cluster>();
        private bool _disposed;
        private readonly ClusterOptions _clusterOptions;
        private CancellationTokenSource _configTokenSource;
        private readonly ConcurrentDictionary<string, IBucket> _bucketRefs = new ConcurrentDictionary<string, IBucket>();
        private readonly ConfigContext _couchbaseContext;
        private BucketConfig _clusterConfig;
        private bool _hasBootStrapped;
        private readonly SemaphoreSlim _bootstrapLock = new SemaphoreSlim(1);

        private readonly Lazy<IQueryClient> _lazyQueryClient;
        private readonly Lazy<ISearchClient> _lazySearchClient;
        private readonly Lazy<IAnalyticsClient> _lazyAnalyticsClient;
        private readonly Lazy<IUserManager> _lazyUserManager;
        private readonly Lazy<IBucketManager> _lazyBucketManager;
        private readonly Lazy<IQueryIndexManager> _lazyQueryManager;
        private readonly Lazy<ISearchIndexManager> _lazySearchManager;

        public Cluster(string connectionString, ClusterOptions clusterOptions)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidConfigurationException("The connectionString cannot be null, empty or only be whitesapce.");
            }
            if (clusterOptions == null)
            {
                throw new InvalidConfigurationException("ClusterOptions is null.");
            }
            if (string.IsNullOrWhiteSpace(clusterOptions.Password) || string.IsNullOrWhiteSpace(clusterOptions.UserName))
            {
                throw new InvalidConfigurationException("Username and password are required.");
            }

            //TODO make connectionString function per the RFC: https://github.com/couchbaselabs/sdk-rfcs/blob/master/rfc/0011-connection-string.md
            clusterOptions.WithServers(connectionString);

            _configTokenSource = new CancellationTokenSource();
            _clusterOptions = clusterOptions;
            _couchbaseContext = new ConfigContext(_clusterOptions);
            _couchbaseContext.Start(_configTokenSource);
            if (_clusterOptions.EnableConfigPolling)
            {
                _couchbaseContext.Poll(_configTokenSource.Token);
            }

            _lazyQueryClient = new Lazy<IQueryClient>(() => new QueryClient(_clusterOptions));
            _lazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => new AnalyticsClient(_clusterOptions));
            _lazySearchClient = new Lazy<ISearchClient>(() => new SearchClient(_clusterOptions));
            _lazyQueryManager = new Lazy<IQueryIndexManager>(() => new QueryIndexManager(_lazyQueryClient.Value));
            _lazyBucketManager = new Lazy<IBucketManager>(() => new BucketManager(_clusterOptions));
            _lazyUserManager = new Lazy<IUserManager>(() => new UserManager(_clusterOptions));
            _lazySearchManager = new Lazy<ISearchIndexManager>(() => new SearchIndexManager(_clusterOptions));
        }

        public Cluster(string connectionStr, string username, string password)
        {
            var connectionString = ConnectionString.Parse(connectionStr);

            _clusterOptions = new ClusterOptions()
                .WithServers(connectionString.Hosts.ToArray())
                .WithCredentials(username, password);

            if (!_clusterOptions.Servers.Any())
            {
                _clusterOptions = _clusterOptions.WithServers("couchbase://localhost");
            }
            _couchbaseContext = new ConfigContext(_clusterOptions);
            _couchbaseContext.Start(_configTokenSource);

            if (_clusterOptions.EnableConfigPolling)
            {
                _couchbaseContext.Poll(_configTokenSource.Token);
            }

            _lazyQueryClient = new Lazy<IQueryClient>(() => new QueryClient(_clusterOptions));
            _lazyAnalyticsClient = new Lazy<IAnalyticsClient>(() => new AnalyticsClient(_clusterOptions));
            _lazySearchClient = new Lazy<ISearchClient>(() => new SearchClient(_clusterOptions));
            _lazyQueryManager = new Lazy<IQueryIndexManager>(() => new QueryIndexManager(_lazyQueryClient.Value));
            _lazyBucketManager = new Lazy<IBucketManager>(() => new BucketManager(_clusterOptions));
            _lazyUserManager = new Lazy<IUserManager>(() => new UserManager(_clusterOptions));
        }

        public static ICluster Connect(string connectionString, ClusterOptions options)
        {
            using (new SynchronizationContextExclusion())
            {
                var cluster = new Cluster(connectionString, options);
                cluster.InitializeAsync().GetAwaiter().GetResult();
                return cluster;
            }
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

        private async Task<ClusterNode> GetClusterNode(IPEndPoint endPoint, Uri uri)
        {
            var connection = endPoint.GetConnection(_clusterOptions);

            var serverFeatures = await connection.Hello().ConfigureAwait(false);
            var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
            await connection.Authenticate(_clusterOptions, null).ConfigureAwait(false);

            var clusterNode = new ClusterNode
            {
                EndPoint = endPoint,
                Connection = connection,
                ServerFeatures = serverFeatures,
                ErrorMap = errorMap,
                ClusterOptions = _clusterOptions,
                BootstrapUri = uri
            };

            return clusterNode;
        }

        internal async Task InitializeAsync()
        {
            //try to connect via GCCP
            foreach (var uri in _clusterOptions.Servers)
            {
                try
                {
                    var endPoint = uri.GetIpEndPoint(11210, false);
                    var bootstrapNode = await GetClusterNode(endPoint, uri).ConfigureAwait(false);

                    //note this returns bucketConfig, but clusterConfig will be returned once server supports GCCP
                    _clusterConfig = await bootstrapNode.GetClusterMap().ConfigureAwait(false);
                    if (_clusterConfig == null)//TODO fix bug NCBC-1966 - hiding XError when no error map (and others)
                    {
                        //No GCCP but we connected - save connections and info for connecting later
                        _clusterOptions.GlobalNodes.Add(bootstrapNode);
                    }
                    else
                    {
                        foreach (var nodesExt in _clusterConfig.GetNodes())
                        {
                            //This is the bootstrap node so we update it
                            if (uri.Host == nodesExt.Hostname)
                            {
                                bootstrapNode.NodesAdapter = nodesExt;
                                bootstrapNode.BuildServiceUris();
                                _clusterOptions.GlobalNodes.Add(bootstrapNode);
                            }
                            else
                            {
                                endPoint = IpEndPointExtensions.GetEndPoint(nodesExt.Hostname, 11210);
                                var clusterNode = await GetClusterNode(endPoint, uri).ConfigureAwait(false);
                                clusterNode.NodesAdapter = nodesExt;
                                clusterNode.BuildServiceUris();
                                _clusterOptions.GlobalNodes.Add(clusterNode);
                            }
                        }

                        // get cluster capabilities
                        UpdateClusterCapabilities(_clusterConfig.GetClusterCapabilities());
                        _hasBootStrapped = true;
                    }
                }
                catch (AuthenticationException e)
                {
                    //auth failed so bubble up exception and clean up resources
                    Log.LogError(e, @"Could not authenticate user {_clusterOptions.UserName}");

                    while (_clusterOptions.GlobalNodes.TryTake(out IClusterNode clusterNode))
                    {
                        clusterNode.Dispose();
                    }

                    throw;
                }
            }
        }

        public async Task<IBucket> BucketAsync(string name)
        {
            //fetching a bucket that has already been opened and cached
            if (_bucketRefs.TryGetValue(name, out var bucket))
            {
                return bucket;
            }

            //Loop through configured list of bootstrap servers
            foreach (var server in _clusterOptions.Servers)
            {
                try
                {
                    bucket = await BootstrapBucketAsync(name, server, BucketType.Couchbase)
                        .ConfigureAwait(false);
                }
                catch
                {
                    bucket = await BootstrapBucketAsync(name, server, BucketType.Memcached)
                        .ConfigureAwait(false);
                }

            }

            //TODO add 3rd state when connected and have GCCP
            if (bucket == null)
            {
                throw new BucketMissingException(@"{name} Bucket not found!");
            }

            _hasBootStrapped = true;
            return bucket;
        }

        private async Task<IBucket> BootstrapBucketAsync(string bucketName, Uri bootstrapUri, BucketType bucketType)
        {
            //Check for any available GC3P connections
            var bootstrapNode = _clusterOptions.GlobalNodes.FirstOrDefault(x =>
                x.Owner == null && x.EndPoint.Address.Equals(bootstrapUri.GetIpAddress(false)));

            //If not create a new connection but don't add to list until it has an owner
            if (bootstrapNode == null)
            {
                var endPoint = bootstrapUri.GetIpEndPoint(11210, false);
                bootstrapNode = await GetClusterNode(endPoint, bootstrapUri).ConfigureAwait(false);

                //Add to global nodes but its no longer a GC3P node since it has an owner
                if (!_clusterOptions.GlobalNodes.Contains(bootstrapNode))
                {
                    _clusterOptions.GlobalNodes.Add(bootstrapNode);
                }
            }

            BucketBase bucket;
            switch (bucketType)
            {
                case BucketType.Couchbase:
                case BucketType.Ephemeral:
                    bucket = new CouchbaseBucket(bucketName, _clusterOptions, _couchbaseContext);
                    break;
                case BucketType.Memcached:
                    bucket = new MemcachedBucket(bucketName, _clusterOptions, _couchbaseContext);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(bucketType), bucketType, null);
            }

            try
            {
                await bucket.Bootstrap(bootstrapNode).ConfigureAwait(false);
            }
            catch (Exception)
            {
                _couchbaseContext.Unsubscribe(bucket);
                throw;
            }

            _bucketRefs.TryAdd(bucketName, bucket);

            return bucket;
        }

        public Task<IDiagnosticsReport> DiagnosticsAsync(string reportId)
        {
            throw new NotImplementedException();
        }

        public Task<IClusterManager> ClusterManagerAsync()
        {
            throw new NotImplementedException();
        }

        private async Task EnsureBootstrapped()
        {
            if (_hasBootStrapped)
            {
                return;
            }

            // if no buckets registered in cluster, throw exception
            if (!_clusterOptions.Buckets.Any())
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
                await BucketAsync(_clusterOptions.Buckets.First());
            }
            finally
            {
                _bootstrapLock.Release(1);
            }
        }

        #region Query

        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions options)
        {
            await EnsureBootstrapped();

            return await _lazyQueryClient.Value.QueryAsync<T>(statement, options);
        }

        #endregion

        #region Analytics

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions options = default)
        {
            await EnsureBootstrapped();

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

            query.ConfigureLifespan(30); //TODO: use clusterOptions.AnalyticsTimeout

            return await _lazyAnalyticsClient.Value.QueryAsync<T>(query, options.CancellationToken);
        }

        #endregion

        #region Search

        public async Task<ISearchResult> SearchQueryAsync(string indexName, SearchQuery query, ISearchOptions options = default)
        {
            await EnsureBootstrapped();

            query.Index = indexName;

            if (options == default)
            {
                options = new SearchOptions();
            }
            //TODO: convert options to params

            return await _lazySearchClient.Value.QueryAsync(query);
        }

        #endregion

        public IQueryIndexManager QueryIndexes => _lazyQueryManager.Value;

        public IAnalyticsIndexManager AnalyticsIndexes { get; }
        public ISearchIndexManager SearchIndexes => _lazySearchManager.Value;

        public IBucketManager Buckets => _lazyBucketManager.Value;

        public IUserManager Users => _lazyUserManager.Value;

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

            _disposed = true;
        }

        /// <inheritdoc />
        public string ExportDeferredAnalyticsQueryHandle<T>(IAnalyticsDeferredResultHandle<T> handle)
        {
            return _lazyAnalyticsClient.Value.ExportDeferredQueryHandle(handle);
        }

        /// <inheritdoc />
        public IAnalyticsDeferredResultHandle<T> ImportDeferredAnalyticsQueryHandle<T>(string encodedHandle)
        {
            return _lazyAnalyticsClient.Value.ImportDeferredQueryHandle<T>(encodedHandle);
        }

        internal void UpdateClusterCapabilities(ClusterCapabilities clusterCapabilities)
        {
            if (_lazyQueryClient.Value is QueryClient client)
            {
                client.UpdateClusterCapabilities(clusterCapabilities);
            }
        }
    }
}
