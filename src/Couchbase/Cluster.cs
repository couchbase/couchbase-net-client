using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Logging;
using Couchbase.Diagnostics;
using Couchbase.Core.Retry;
using Couchbase.Core.Retry.Query;
using Couchbase.Core.Retry.Search;
using Couchbase.Management.Analytics;
using Couchbase.Management.Buckets;
using Couchbase.Management.Eventing;
using Couchbase.Management.Query;
using Couchbase.Management.Search;
using Couchbase.Management.Users;
using Couchbase.Query;
using Couchbase.Search;
using Microsoft.Extensions.Logging;
using AnalyticsOptions = Couchbase.Analytics.AnalyticsOptions;
using Couchbase.Core.RateLimiting;
using Couchbase.Search.Queries.Simple;
using Couchbase.Search.Queries.Vector;

#nullable enable

namespace Couchbase
{
    public class Cluster : ICluster, IBootstrappable
    {
        internal const string RequiresUnreferencedCodeMessage =
            "The Couchbase SDK might require types that cannot be statically analyzed. Make sure all required types are preserved.";
        internal const string RequiresDynamicCodeMessage =
            "The Couchbase SDK might require types that cannot be statically analyzed and might need runtime code generation. Do not use for native AOT applications.";

        private readonly ILogger<Cluster> _logger;
        private readonly IRetryOrchestrator _retryOrchestrator;
        private readonly object _syncObject = new object();
        private bool _disposed;
        private readonly ClusterContext _context;
        private bool _hasBootStrapped = false;
        private readonly SemaphoreSlim _bootstrapLock = new SemaphoreSlim(1);
        private readonly IRedactor _redactor;
        private readonly IBootstrapper _bootstrapper;
        private readonly List<Exception> _deferredExceptions = new List<Exception>();
        private volatile ClusterState _clusterState;
        private readonly IRequestTracer _tracer;
        private readonly IRetryStrategy _retryStrategy;
        private readonly MeterForwarder? _meterForwarder;

        // Internal is used to provide a seam for unit tests
        internal LazyService<IQueryClient> LazyQueryClient;
        internal LazyService<ISearchClient> LazySearchClient;
        internal LazyService<IAnalyticsClient> LazyAnalyticsClient;
        internal LazyService<IUserManager> LazyUserManager;
        internal LazyService<IBucketManager> LazyBucketManager;
        internal LazyService<IQueryIndexManager> LazyQueryManager;
        internal LazyService<ISearchIndexManager> LazySearchManager;
        internal LazyService<IAnalyticsIndexManager> LazyAnalyticsIndexManager;
        internal LazyService<IEventingFunctionManager> LazyEventingFunctionManager;

        internal Cluster(ClusterOptions clusterOptions)
        {
            if (clusterOptions == null)
            {
                throw new InvalidConfigurationException("ClusterOptions is null.");
            }
            if (string.IsNullOrWhiteSpace(clusterOptions.Password) || string.IsNullOrWhiteSpace(clusterOptions.UserName))
            {
                throw new InvalidConfigurationException("Username and password are required.");
            }

            var configTokenSource = new CancellationTokenSource();
            _context = new ClusterContext(this, configTokenSource, clusterOptions);
            _context.Start();

            LazyQueryClient = new LazyService<IQueryClient>(_context.ServiceProvider);
            LazyAnalyticsClient = new LazyService<IAnalyticsClient>(_context.ServiceProvider);
            LazySearchClient = new LazyService<ISearchClient>(_context.ServiceProvider);
            LazyQueryManager = new LazyService<IQueryIndexManager>(_context.ServiceProvider);
            LazyBucketManager = new LazyService<IBucketManager>(_context.ServiceProvider);
            LazyUserManager = new LazyService<IUserManager>(_context.ServiceProvider);
            LazySearchManager = new LazyService<ISearchIndexManager>(_context.ServiceProvider);
            LazyAnalyticsIndexManager = new LazyService<IAnalyticsIndexManager>(_context.ServiceProvider);
            LazyEventingFunctionManager = new LazyService<IEventingFunctionManager>(_context.ServiceProvider);

            _logger = _context.ServiceProvider.GetRequiredService<ILogger<Cluster>>();
            _retryOrchestrator = _context.ServiceProvider.GetRequiredService<IRetryOrchestrator>();
            _redactor = _context.ServiceProvider.GetRequiredService<IRedactor>();
            _tracer = _context.ServiceProvider.GetRequiredService<IRequestTracer>();
            _retryStrategy = _context.ServiceProvider.GetRequiredService<IRetryStrategy>();

            var meter = _context.ServiceProvider.GetRequiredService<IMeter>();
            if (meter is not NoopMeter)
            {
                // Don't instantiate the meter forwarder if we're using the NoopMeter, since the meter forwarder
                // will create subscriptions to the .NET metrics and start collecting/forwarding data. We can avoid
                // this performance penalty when we know we're doing nothing with the data.
                _meterForwarder = new MeterForwarder(meter);
            }

            var bootstrapperFactory = _context.ServiceProvider.GetRequiredService<IBootstrapperFactory>();
            _bootstrapper = bootstrapperFactory.Create(clusterOptions.BootstrapPollInterval);
        }

        /// <inheritdoc />
        public IServiceProvider ClusterServices => _context.ServiceProvider;

        #region Connect

        [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public static Task<ICluster> ConnectAsync(string connectionString, Action<ClusterOptions> configureOptions)
        {
            var options = new ClusterOptions();
            configureOptions.Invoke(options);

            return ConnectAsync(connectionString, options);
        }

        [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public static Task<ICluster> ConnectAsync(string connectionString, ClusterOptions? options = null)
        {
            return ConnectAsync((options ?? new ClusterOptions()).WithConnectionString(connectionString));
        }

        [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public static Task<ICluster> ConnectAsync(string connectionString, string username, string password)
        {
            return ConnectAsync(connectionString, new ClusterOptions
            {
                UserName = username,
                Password = password
            });
        }

        [RequiresUnreferencedCode(RequiresUnreferencedCodeMessage)]
        [RequiresDynamicCode(RequiresDynamicCodeMessage)]
        public static async Task<ICluster> ConnectAsync(ClusterOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var connectionString = options.ConnectionStringValue ?? throw new ArgumentException(
                $"{nameof(options)} must have a connection string.", nameof(options));
            var schema = connectionString.Scheme;

#if NETCOREAPP3_1_OR_GREATER
            if (schema is Scheme.Couchbase2)
            {
                //Couchbase CNG clusters
                return await Stellar.StellarCluster.ConnectAsync(options).ConfigureAwait(false);
            }
#endif
            //Couchbase on-premise and cappella clusters
            //try to bootstrap for GC3P; if pre-6.6 this will fail and bootstrapping will
            //happen at the bucket level. In this case the caller can open a bucket and
            //resubmit the cluster level request.
            var cluster = new Cluster(options);
            await ((IBootstrappable)cluster).BootStrapAsync().ConfigureAwait(false);
            cluster.StartBootstrapper();
            return cluster;
        }

        #endregion

        #region Bucket

        public ValueTask<IBucket> BucketAsync(string name)
        {
            if (IsBootstrapped)
            {
                if (_hasBootStrapped)
                {
                    // The most common path is an already bootstrapped cluster, so we want to avoid
                    // heap allocations along that path using ValueTask. Since _hasBootStrapped is already
                    // set to true, we don't need to await GetOrCreateBucketAsync here just to set it to true again.
                    // By avoiding the await we also avoid some heap allocations for tasks and continuations.

                    return _context.GetOrCreateBucketAsync(name);
                }

                return new ValueTask<IBucket>(GetOrCreateBucketAsync(name));
            }

            var message = _deferredExceptions.Count == 0
                ? "Cluster has not yet bootstrapped. Call WaitUntilReadyAsync(..) to wait for it to complete."
                : "The Cluster cannot bootstrap. Check the inner exceptions for details.";

            return new ValueTask<IBucket>(
                Task.FromException<IBucket>(new AggregateException(message, _deferredExceptions)));
        }

        private async Task<IBucket> GetOrCreateBucketAsync(string name)
        {
            // This is a separate method rather than a lambda within BucketAsync to prevent allocating
            // a closure in the hot path where we are already bootstrapped.

            var bucket = await _context.GetOrCreateBucketAsync(name).ConfigureAwait(false);
            _hasBootStrapped = true; //for legacy pre-6.5 servers
            return bucket;
        }

        #endregion

        #region Diagnostics

        /// <inheritdoc />
        public async Task<IDiagnosticsReport> DiagnosticsAsync(DiagnosticsOptions? options = null)
        {
            options ??= new DiagnosticsOptions();
            return await DiagnosticsReportProvider.CreateDiagnosticsReportAsync(_context, options.ReportIdValue ?? Guid.NewGuid().ToString())
                .ConfigureAwait(false);
        }

        public async Task<IPingReport> PingAsync(PingOptions? options = null)
        {
            options ??= new PingOptions();

            return await DiagnosticsReportProvider.
               CreatePingReportAsync(_context, _context.GlobalConfig, options).
               ConfigureAwait(false);
        }

        /// <summary>
        /// Waits until bootstrapping has completed and all services have been initialized.
        /// </summary>
        /// <param name="timeout">The amount of time to wait for the desired <see cref="ClusterState"/>.</param>
        /// <param name="options">The optional arguments.</param>
        public async Task WaitUntilReadyAsync(TimeSpan timeout, WaitUntilReadyOptions? options = null)
        {
            options ??= new WaitUntilReadyOptions();
            if(options.DesiredStateValue == ClusterState.Offline)
                throw new ArgumentException(nameof(options.DesiredStateValue));

            var token = options.CancellationTokenValue;
            CancellationTokenSource? tokenSource = null;
            if (token == CancellationToken.None)
            {
                tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                tokenSource.CancelAfter(timeout);
                token = tokenSource.Token;
            }

            var bootstrappable = (IBootstrappable)this;
            try
            {
                token.ThrowIfCancellationRequested();
                while (!token.IsCancellationRequested)
                {
                    if (!IsBootstrapped)
                    {
                        await bootstrappable.BootStrapAsync().ConfigureAwait(false);
                        if (!IsBootstrapped)
                        {
                            await Task.Delay(100, token).ConfigureAwait(false);
                            continue;
                        }
                    }


                    if (!_context.IsGlobal)
                        throw new NotSupportedException(
                            "Cluster level WaitUntilReady is only supported by Couchbase Server 6.5 or greater. " +
                            "If you think this exception is caused by another error, please check your SDK logs for detail.");

                    var pingReport =
                        await DiagnosticsReportProvider.CreatePingReportAsync(_context, _context.GlobalConfig,
                            new PingOptions
                            {
                                ServiceTypesValue = options.EffectiveServiceTypes(_context).ToList()
                            }).ConfigureAwait(false);

                    var status = new Dictionary<string, bool>();
                    foreach (var service in pingReport.Services)
                    {
                        var failures = service.Value.Any(x => x.State != ServiceState.Ok);
                        if (failures)
                        {
                            //mark a service as failed
                            status.Add(service.Key, failures);
                        }
                    }

                    //everything is up
                    if (status.Count == 0)
                    {
                        _clusterState = ClusterState.Online;
                        return;
                    }

                    //determine if completely offline or degraded
                    _clusterState = status.Count == pingReport.Services.Count ? ClusterState.Offline : ClusterState.Degraded;
                    if(_clusterState == options.DesiredStateValue)
                    {
                        return;
                    }

                    await Task.Delay(100, token).ConfigureAwait(false);
                }

                if (token.IsCancellationRequested)
                {
                    throw new UnambiguousTimeoutException($"Timed out after {timeout} with {_context.Nodes.Count} nodes.");
                }
            }
            catch (RateLimitedException)
            {
                throw;
            }
            catch (NotSupportedException)
            {
                throw;
            }
            catch (OperationCanceledException e)
            {
                throw new UnambiguousTimeoutException($"Timed out after {timeout} with {_context.Nodes.Count} nodes.", e);
            }

            catch (Exception e)
            {
                throw new CouchbaseException("An error has occurred, see the inner exception for details.", e);
            }
            finally
            {
                tokenSource?.Dispose();
            }
        }

        #endregion

        #region Query

        public async Task<IQueryResult<T>> QueryAsync<T>(string statement, QueryOptions? options = null)
        {
            options ??= new QueryOptions();
            options.TimeoutValue ??= _context.ClusterOptions.QueryTimeout;

            async Task<IQueryResult<T>> Func()
            {
                var client1 = LazyQueryClient.GetValueOrThrow();
                var statement1 = statement;
                var options1 = options!;
                return await client1.QueryAsync<T>(statement1, options1).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, new QueryRequest
            {
                Options = options,
                Statement = statement,
                Token = options.Token,
                Timeout = options.TimeoutValue.GetValueOrDefault(),
                RetryStrategy =  options.RetryStrategyValue ?? _retryStrategy
            }).ConfigureAwait(false);
        }

        #endregion

        #region Analytics

        public async Task<IAnalyticsResult<T>> AnalyticsQueryAsync<T>(string statement, AnalyticsOptions? options = default)
        {
            options ??= new AnalyticsOptions();

            if (!options.TimeoutValue.HasValue)
            {
                options.Timeout(_context.ClusterOptions.AnalyticsTimeout);
            }

            if (string.IsNullOrWhiteSpace(options.ClientContextIdValue))
            {
                options.ClientContextId(Guid.NewGuid().ToString());
            }

            ThrowIfNotBootstrapped();

            if(options.RetryStrategyValue == null)
            {
                options.RetryStrategy(_retryStrategy);
            }

            async Task<IAnalyticsResult<T>> Func()
            {
                var client1 = LazyAnalyticsClient.GetValueOrThrow();
                var options1 = options;
                return await client1.QueryAsync<T>(statement, options1).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, AnalyticsRequest.Create(statement, options)).ConfigureAwait(false);
        }

        #endregion

        #region Search

        public async Task<ISearchResult> SearchQueryAsync(string indexName, ISearchQuery query, SearchOptions? options = default)
        {
            var searchRequest = SearchRequest.Create(query);
            return await SearchAsync(indexName, searchRequest, options).ConfigureAwait(false);
        }

        public async Task<ISearchResult> SearchAsync(
            string indexName,
            SearchRequest searchRequest,
            SearchOptions? options = default)
        {
            options ??= new SearchOptions();
            options.TimeoutValue ??= _context.ClusterOptions.SearchTimeout;

            ThrowIfNotBootstrapped();
            // The RFC asks to wait until the GlobalConfig is ready.  That should be handled by ThrowIfNotBootstrapped.
            if (searchRequest.Scope is not null)
            {
                _context.GlobalConfig?.AssertClusterCap(ClusterCapabilities.SCOPED_SEARCH_INDEX,
                    "This API is for use with scoped indexes, which require Couchbase Server 7.6.0 or above, with at least one search node");
            }

            if (searchRequest.VectorSearch is not null)
            {
                _context.GlobalConfig?.AssertClusterCap(ClusterCapabilities.VECTOR_SEARCH,
                    "Vector queries are available from Couchbase Server 7.6.0 and above, with at least one search node");
            }

            FtsSearchRequest ftsSearchRequest = new()
            {
                Index = indexName,
                Query = searchRequest.SearchQuery,
                Options = options,
                Token = options.Token,
                Timeout = options.TimeoutValue.Value,
                RetryStrategy = options.RetryStrategyValue ?? _retryStrategy
            };

            async Task<ISearchResult> Func()
            {
                var client1 = LazySearchClient.GetValueOrThrow();
                return await client1.QueryAsync(indexName, ftsSearchRequest, searchRequest.VectorSearch, searchRequest.Scope, options.Token).ConfigureAwait(false);
            }

            return await _retryOrchestrator.RetryAsync(Func, ftsSearchRequest).ConfigureAwait(false);
        }
        #endregion

        #region Management

        /// <inheritdoc />
        public IQueryIndexManager QueryIndexes => LazyQueryManager.GetValueOrThrow();

        /// <inheritdoc />
        public IAnalyticsIndexManager AnalyticsIndexes => LazyAnalyticsIndexManager.GetValueOrThrow();

        /// <inheritdoc />
        public ISearchIndexManager SearchIndexes => LazySearchManager.GetValueOrThrow();

        /// <inheritdoc />
        public IBucketManager Buckets => LazyBucketManager.GetValueOrThrow();

        /// <inheritdoc />
        public IUserManager Users => LazyUserManager.GetValueOrThrow();

        /// <inheritdoc />
        public IEventingFunctionManager EventingFunctions => LazyEventingFunctionManager.GetValueOrThrow();

        #endregion

        #region Misc

        internal void UpdateClusterCapabilities()
        {
            if (_context.GlobalConfig is not null)
            {
                // Client may be null if the service was not registered
                LazyQueryClient.Value?.UpdateClusterCapabilities(_context.GlobalConfig.GetClusterCapabilities());
            }
        }

        #endregion

        #region Bootstrapping error handling and propagation

        internal void StartBootstrapper()
        {
            _bootstrapper.Start(this);
        }

        async Task IBootstrappable.BootStrapAsync()
        {
            try
            {
                await _context.BootstrapGlobalAsync().ConfigureAwait(false);

                //if we succeeded set the state of the cluster to bootstrapped
                _hasBootStrapped = _context.GlobalConfig != null;
                _deferredExceptions.Clear();

                UpdateClusterCapabilities();
            }
            catch (AuthenticationFailureException e)
            {
                _deferredExceptions.Add(e);

                //auth failed so bubble up exception and clean up resources
                _logger.LogError(e,
                    "Could not authenticate user {username}",
                    _redactor.UserData(_context.ClusterOptions.UserName ?? string.Empty));

                _context.RemoveAllNodes();
                throw;
            }
            catch (Exception e)
            {
                _deferredExceptions.Add(e);
                _logger.LogDebug("Error encountered bootstrapping cluster; if the cluster is 6.5 or earlier, this can be ignored. {exception}.", e);
            }
        }

        private bool IsBootstrapped => _deferredExceptions.Count == 0 && _context.Nodes.Count > 0;
        bool IBootstrappable.IsBootstrapped => IsBootstrapped;

        List<Exception> IBootstrappable.DeferredExceptions => _deferredExceptions;

        private void ThrowIfNotBootstrapped()
        {
            if (this is IBootstrappable b)
            {
                if (!b.IsBootstrapped && b.DeferredExceptions.Any())
                {
                    throw new AggregateException("Cannot bootstrap cluster.", _deferredExceptions);
                }
            }
        }

        /// <summary>
        /// Seam for unit tests and for supporting non-GC3P servers (prior to v6.5).
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

            await _bootstrapLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // check again to make sure we still need to bootstrap
                if (_hasBootStrapped)
                {
                    return;
                }

                // try to bootstrap first bucket in cluster; we need this because pre-6.5 servers do not support GC3P so we need to open a bucket
                var bucketName = _context.ClusterOptions.Buckets.First();
                _logger.LogDebug("Attempting to bootstrap bucket {bucketname}", _redactor.MetaData(bucketName));
                await BucketAsync(bucketName).ConfigureAwait(false);
                UpdateClusterCapabilities();
            }
            finally
            {
                _bootstrapLock.Release(1);
            }
        }

        #endregion

        #region Dispose

        /// <inheritdoc />
        public void Dispose()
        {
            if (_disposed) return;
            lock (_syncObject)
            {
                if (_disposed) return;
                _disposed = true;
                _bootstrapper.Dispose();
                _context.Dispose();
                _meterForwarder?.Dispose();
            }
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            try
            {
                Dispose();
                return default;
            }
            catch (Exception ex)
            {
                return new ValueTask(Task.FromException(ex));
            }
        }

        #endregion
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/
