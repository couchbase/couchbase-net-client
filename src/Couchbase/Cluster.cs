using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics;
using Couchbase.Core.Logging;
using Couchbase.Management;
using Couchbase.Services.Analytics;
using Couchbase.Services.Query;
using Couchbase.Services.Search;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    public class Cluster : ICluster
    {
        private static readonly ILogger Log = LogManager.CreateLogger<Cluster>();
        private bool _disposed;
        private readonly Configuration _configuration;
        private IQueryClient _queryClient;
        private ISearchClient _searchClient;
        private IAnalyticsClient _analyticsClient;
        private CancellationTokenSource _configTokenSource;
        private readonly ConcurrentDictionary<string, IBucket> _bucketRefs = new ConcurrentDictionary<string, IBucket>();
        private readonly ConfigContext _configContext;

        public Cluster(Configuration configuration)
        {
            if (configuration == null)
            {
                throw new InvalidConfigurationException("Configuration is null.");
            }

            if (string.IsNullOrWhiteSpace(configuration.Password) || string.IsNullOrWhiteSpace(configuration.UserName))
            {
                throw new InvalidConfigurationException("Username and password are required.");
            }

            _configTokenSource = new CancellationTokenSource();
            _configuration = configuration;
            _configContext = new ConfigContext(_configuration);
            _configContext.Start(_configTokenSource);
            if (_configuration.EnableConfigPolling)
            {
                _configContext.Poll(_configTokenSource.Token);
            }
        }

        public Cluster(string connectionStr, string username, string password)
        {
            var connectionString = ConnectionString.Parse(connectionStr);

            _configuration = new Configuration()
                .WithServers(connectionString.Hosts.ToArray())
                .WithCredentials(username, password);

            if (!_configuration.Buckets.Any())
            {
                _configuration = _configuration.WithBucket("default");
            }

            if (!_configuration.Servers.Any())
            {
                _configuration = _configuration.WithServers("couchbase://localhost");
            }
            _configContext = new ConfigContext(_configuration);
            _configContext.Start(_configTokenSource);

            if (_configuration.EnableConfigPolling)
            {
                _configContext.Poll(_configTokenSource.Token);
            }
        }

        private async Task<ClusterNode> GetClusterNode(IPEndPoint endPoint)
        {
            var connection = endPoint.GetConnection();

            var serverFeatures = await connection.Hello().ConfigureAwait(false);
            var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
            await connection.Authenticate(_configuration, null).ConfigureAwait(false);

            var clusterNode = new ClusterNode
            {
                EndPoint = endPoint,
                Connection = connection,
                ServerFeatures = serverFeatures,
                ErrorMap = errorMap
            };

            return clusterNode;
        }

        public async Task Initialize()
        {
            //try to connect via GCCP
            foreach (var uri in _configuration.Servers)
            {
                try
                {
                    var endPoint = uri.GetIpEndPoint(11210, false);
                    var bootstrapNode = await GetClusterNode(endPoint).ConfigureAwait(false);

                    //note this returns bucketConfig, but clusterConfig will be returned once server supports GCCP
                    var clusterMap = await bootstrapNode.GetClusterMap().ConfigureAwait(false);
                    if (clusterMap == null)//TODO fix bug NCBC-1966 - hiding XError when no error map (and others)
                    {
                        //No GCCP but we connected - save connections and info for connecting later
                        _configuration.GlobalNodes.Add(bootstrapNode);
                    }
                    else
                    {
                        //TODO add GCCP path when it exists on Mad Hatter builds
                        //Store the ClusterConfig (global) and then build the nodes
                    }
                }
                catch (AuthenticationException e)
                {
                    //auth failed so bubble up exception and clean up resources
                    Log.LogError(e, @"Could not authenticate user {_configuration.UserName}");

                    while (_configuration.GlobalNodes.TryTake(out ClusterNode clusterNode))
                    {
                        clusterNode.Dispose();
                    }

                    throw;
                }
            }
        }

        public async Task<IBucket> Bucket(string name)
        {
            if (_bucketRefs.TryGetValue(name, out var bucket))
            {
                return bucket;
            }

            //No GCCP but we have a connection - not bootstrapped yet so commence strapping das boot
            var bootstrapNode = _configuration.GlobalNodes.FirstOrDefault(x => x.Owner == null);
            if (bootstrapNode == null)
            {
                //use existing clusterNode from bootstrapping
                bucket = new CouchbaseBucket(name, _configuration, _configContext);
                _configContext.Subscribe((IBucketInternal)bucket);
                await ((IBucketInternal) bucket).Bootstrap(bootstrapNode).ConfigureAwait(false);

                _bucketRefs.TryAdd(name, bucket);
            }
            else
            {
                //all clusterNodes have owners so create a new one
                var uri = _configuration.Servers.GetRandom();
                var endPoint = uri.GetIpEndPoint(11210, false);
                bootstrapNode = await GetClusterNode(endPoint).ConfigureAwait(false);

                bucket = new CouchbaseBucket(name, _configuration, _configContext);
                _configContext.Subscribe((IBucketInternal)bucket);
                await ((IBucketInternal) bucket).Bootstrap(bootstrapNode).ConfigureAwait(false);

                _configuration.GlobalNodes.Add(bootstrapNode);
                _bucketRefs.TryAdd(name, bucket);
            }

            //TODO add 3rd state when connected and have GCCP
            if (bucket == null)
            {
                throw new BucketMissingException(@"{name} Bucket not found!");
            }

            return bucket;
        }

        public Task<IDiagnosticsReport> Diagnostics(string reportId)
        {
            throw new NotImplementedException();
        }

        public Task<IClusterManager> ClusterManager()
        {
            throw new NotImplementedException();
        }

        public Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryParameter parameters = null,
            IQueryOptions options = null)
        {
            if (_queryClient == null) _queryClient = new QueryClient(_configuration);

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

        /// <inheritdoc />
        public string ExportDeferredAnalyticsQueryHandle<T>(IAnalyticsDeferredResultHandle<T> handle)
        {
            return _analyticsClient.ExportDeferredQueryHandle(handle);
        }

        /// <inheritdoc />
        public IAnalyticsDeferredResultHandle<T> ImportDeferredAnalyticsQueryHandle<T>(string encodedHandle)
        {
            return _analyticsClient.ImportDeferredQueryHandle<T>(encodedHandle);
        }
    }
}
