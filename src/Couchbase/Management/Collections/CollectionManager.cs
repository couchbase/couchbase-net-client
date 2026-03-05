#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Couchbase.Management.Collections
{
    /// <remarks>Volatile</remarks>
    internal class CollectionManager : ICouchbaseCollectionManager
    {
        private readonly string _bucketName;
        private volatile BucketConfig _bucketConfig;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<CollectionManager> _logger;
        private readonly IRedactor _redactor;
        private readonly IAppTelemetryCollector _appTelemetryCollector;
        private readonly IRequestTracer _tracer;

        /// <summary>
        /// REST endpoint path definitions.
        /// </summary>
        // ReSharper disable once MemberCanBePrivate.Global
        internal static class RestApi
        {
            public static string GetScope(string bucketName, string scopeName) =>
                $"pools/default/buckets/{bucketName}/scopes/{scopeName}";

            public static string GetScopes(string bucketName) => $"pools/default/buckets/{bucketName}/scopes";
            public static string CreateScope(string bucketName) => $"pools/default/buckets/{bucketName}/scopes";

            public static string DeleteScope(string bucketName, string scopeName) =>
                $"pools/default/buckets/{bucketName}/scopes/{scopeName}";

            public static string CreateCollections(string bucketName, string scopeName) =>
                $"pools/default/buckets/{bucketName}/scopes/{scopeName}/collections";

            public static string DeleteCollections(string bucketName, string scopeName, string collectionName) =>
                $"pools/default/buckets/{bucketName}/scopes/{scopeName}/collections/{collectionName}";

            public static string UpdateCollection(string bucketName, string scopeName, string collectionName) =>
                $"/pools/default/buckets/{bucketName}/scopes/{scopeName}/collections/{collectionName}";

        }

        public CollectionManager(string bucketName, BucketConfig bucketConfig, IServiceUriProvider serviceUriProvider,
            ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<CollectionManager> logger, IRedactor redactor, IAppTelemetryCollector appTelemetryCollector, IRequestTracer? tracer = null)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _bucketConfig = bucketConfig ?? throw new ArgumentNullException(nameof(bucketConfig));
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _appTelemetryCollector =
                appTelemetryCollector ?? throw new ArgumentNullException(nameof(appTelemetryCollector));
            _tracer = tracer ?? NoopRequestTracer.Instance;
        }

        // ReSharper disable once MemberCanBePrivate.Global
        internal (IClusterNode, Uri) GetUri(string path)
        {
            var managementNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(managementNode.ManagementUri)
            {
                Path = path
            };

            return (managementNode, builder.Uri);
        }

        public async Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions? options = null)
        {
            options ??= CollectionExistsOptions.Default;
            var (_, uri) = GetUri(RestApi.GetScope(_bucketName, spec.ScopeName));
            _logger.LogInformation(
                "Attempting to verify if scope/collection {ScopeName}/{Name} exists - {Uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(options.TokenValue))
                        .ConfigureAwait(false);

                // try find scope / collection
                return scopes.Any(scope =>
                    scope.Name == spec.ScopeName && scope.Collections.Any(collection => collection.Name == spec.Name)
                );
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Failed to verify if collection {ScopeName}/{Name} exists - {Uri}", spec.ScopeName,
                    spec.Name, _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<bool> ScopeExistsAsync(string scopeName, ScopeExistsOptions? options = null)
        {
            options ??= ScopeExistsOptions.Default;
            var (_, uri) = GetUri(RestApi.GetScope(_bucketName, scopeName));
            _logger.LogInformation("Attempting to verify if scope {ScopeName} exists - {Uri}", scopeName,
                _redactor.SystemData(uri));

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(options.TokenValue))
                        .ConfigureAwait(false);

                // try find scope
                return scopes.Any(scope => scope.Name == scopeName);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to verify if scope {ScopeName} exists - {Uri}", scopeName,
                    _redactor.SystemData(uri));
                throw;
            }
        }

        [Obsolete("Use GetAllScopesAsync instead.")]
        public async Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
        {
            options ??= GetScopeOptions.Default;
            var (_, uri) = GetUri(RestApi.GetScope(_bucketName, scopeName));
            _logger.LogInformation("Attempting to verify if scope {ScopeName} exists - {Uri}", scopeName,
                _redactor.SystemData(uri));

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions().CancellationToken(options.TokenValue))
                        .ConfigureAwait(false);

                // try find scope
                var scope = scopes.SingleOrDefault(x => x.Name == scopeName);
                if (scope != null)
                {
                    return scope;
                }

                // throw not found exception
                throw ScopeNotFoundException.FromScopeName(scopeName);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to verify if scope {ScopeName} exists - {Uri}", scopeName,
                    _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
        {
            options ??= GetAllScopesOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.GetScopes(_bucketName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.GetAllScopes, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            _logger.LogInformation("Attempting to get all scopes - {Uri}", _redactor.SystemData(uri));

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                // get manifest
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                //Throw any other error cases
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = result.StatusCode,
                        Message = body,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    result.ThrowIfRateLimitingError(body, ctx);

                    result.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                using var stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var jsonReader = new JsonTextReader(new StreamReader(stream, Encoding.UTF8));

                // check scope & collection exists in manifest
                var json = await JToken.ReadFromAsync(jsonReader, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                var scopes = json.SelectToken("scopes")!;

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);

                return scopes.Select(scope => new ScopeSpec(scope["name"]?.Value<string>())
                {
                    Collections = scope["collections"]!.Select(collection =>
                        new CollectionSpec(scope["name"]?.Value<string>(), collection["name"]?.Value<string>())
                        {
                            MaxExpiry = collection["maxTTL"] == null
                                ? (TimeSpan?)null
                                : TimeSpan.FromSeconds(collection["maxTTL"]!.Value<long>()),
                            History = collection["history"] == null ? null : collection["history"]!.Value<bool>()
                        }
                    ).ToList()
                }).ToList();
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to get all scopes - {Uri}", _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }

        public async Task CreateCollectionAsync(string scopeName, string collectionName,
            CreateCollectionSettings settings, CreateCollectionOptions? options)
        {
            if (settings.History.HasValue &&
                !_bucketConfig.BucketCapabilities.Contains(BucketCapabilities.NON_DEDUPED_HISTORY))
            {
                throw new FeatureNotAvailableException(
                    "CreateCollectionAsync with History parameter is only supported from Server version 7.2");
            }

            options ??= CreateCollectionOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.CreateCollections(_bucketName, scopeName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.CreateCollection, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            _logger.LogInformation("Attempting create collection {ScopeName}/{CollectionName} - {Uri}", scopeName,
                collectionName, _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            using var tracker = MetricTracker.Management.StartOperation(
                OuterRequestSpans.ManagerSpan.Collections.CreateCollection,
                rootSpan,
                _bucketName,
                scopeName,
                collectionName);
            try
            {
                // create collection
                var keys = new Dictionary<string, string>
                {
                    { "name", collectionName }
                };

                if (settings.MaxExpiry.HasValue)
                {
                    keys.Add("maxTTL",
                        ((int)settings.MaxExpiry.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture));
                }

                if (settings.History.HasValue)
                {
                    keys.Add("history", settings.History.Value.ToLowerString());
                }

                var content = new FormUrlEncodedContent(keys!);
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                using var createResult = await httpClient
                    .PostAsync(uri, content, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (createResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await createResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = createResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    createResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("already exists"))
                    {
                        var ex = new CollectionExistsException(scopeName, collectionName)
                        {
                            Context = ctx
                        };
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    if (contentBody.Contains("not found"))
                    {
                        var ex = ScopeNotFoundException.FromScopeName(scopeName);
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    if (contentBody.Contains("The value must be in range from -1 to 2147483647"))
                    {
                        var ex = new InvalidArgumentException(contentBody) { Context = ctx };
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);
            }
            catch (Exception exception)
            {
                tracker.SetError(exception);
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to create collection {ScopeName}/{Name} - {Uri}",
                    scopeName,
                    collectionName, _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }

        public Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null)
        {
            return CreateCollectionAsync(spec.ScopeName, spec.Name,
                new CreateCollectionSettings(spec.MaxExpiry, spec.History), options);
        }

        public async Task DropCollectionAsync(string scopeName, string collectionName,
            DropCollectionOptions? options = null)
        {
            options ??= DropCollectionOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.DeleteCollections(_bucketName, scopeName, collectionName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.DropCollection, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            _logger.LogInformation("Attempting drop collection {Scope}/{Collection} - {Uri}", scopeName,
                collectionName, _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            using var tracker = MetricTracker.Management.StartOperation(
                OuterRequestSpans.ManagerSpan.Collections.DropCollection,
                rootSpan,
                _bucketName,
                scopeName,
                collectionName);
            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                using var createResult = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (createResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await createResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = createResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    createResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("not found"))
                    {
                        var ex = new CollectionNotFoundException(scopeName, collectionName)
                        {
                            Context = ctx
                        };
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);
            }
            catch (Exception exception)
            {
                tracker.SetError(exception);
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to drop collection {Scope}/{Collection} - {Uri}", scopeName,
                    collectionName, _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }

        [Obsolete("Use other overloaded DropCollectionAsync() method that does not take a CollectionSpec instead.")]
        public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null)
        {
            options ??= DropCollectionOptions.Default;
            var (_, uri) = GetUri(RestApi.DeleteCollections(_bucketName, spec.ScopeName, spec.Name));
            _logger.LogInformation("Attempting drop collection {ScopeName}/{Name} - {Uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // drop collection
                using var httpClient = _httpClientFactory.Create();
                using var createResult = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                if (createResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await createResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = createResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    createResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("not found"))
                        throw new CollectionNotFoundException(spec.ScopeName, spec.Name)
                        {
                            Context = ctx
                        };

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop collection {ScopeName}/{Name} - {Uri}",
                    spec.ScopeName,
                    spec.Name, _redactor.SystemData(uri));
                throw;
            }
        }

        /// <summary>
        /// Creates a scope given a unique name.
        /// </summary>
        /// <param name="scopeName">The name of the scope to create.</param>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        public async Task CreateScopeAsync(string scopeName, CreateScopeOptions? options = null)
        {
            options ??= CreateScopeOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.CreateScope(_bucketName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.CreateScope, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            _logger.LogInformation("Attempting create scope {Name} - {Uri}", scopeName, _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;
            // create scope
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "name", scopeName }
            }!);

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            using var tracker = MetricTracker.Management.StartOperation(
                OuterRequestSpans.ManagerSpan.Collections.CreateScope,
                rootSpan,
                _bucketName,
                scopeName);
            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                using var createResult = await httpClient
                    .PostAsync(uri, content, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (createResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await createResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = createResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    createResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("already exists"))
                    {
                        var ex = new ScopeExistsException(scopeName)
                        {
                            Context = ctx
                        };
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);
            }
            catch (Exception exception)
            {
                tracker.SetError(exception);
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to create scope {Name} - {Uri}", scopeName,
                    _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }

        /// <summary>
        /// Creates a scope given a unique name.
        /// </summary>
        /// <param name="spec">The <see cref="ScopeSpec"/> of the scope including its name.</param>
        /// <remarks>Does not create the collections if any are included in the <see cref="ScopeSpec"/></remarks>
        /// <param name="options">Any optional parameters.</param>
        /// <returns>A <see cref="Task"/> that can be awaited.</returns>
        [Obsolete("Use other overloaded CreateScopeAsync method that does not take a ScopeSpec instead.")]
        public Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null)
        {
            return CreateScopeAsync(spec.Name, options);
        }

        public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
        {
            options ??= DropScopeOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.DeleteScope(_bucketName, scopeName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.DropScope, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            _logger.LogInformation("Attempting drop scope {ScopeName} - {Uri}", scopeName, _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            using var tracker = MetricTracker.Management.StartOperation(
                OuterRequestSpans.ManagerSpan.Collections.DropScope,
                rootSpan,
                _bucketName,
                scopeName);
            try
            {
                // drop scope
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                using var createResult = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;
                if (createResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await createResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = createResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    //Throw specific exception if a rate limiting exception is thrown.
                    createResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("not found"))
                    {
                        var ex = ScopeNotFoundException.FromScopeName(scopeName);
                        ex.Context = ctx;
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);
            }
            catch (Exception exception)
            {
                tracker.SetError(exception);
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to drop scope {ScopeName} - {Uri}", scopeName,
                    _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }

        public async Task UpdateCollectionAsync(string scopeName, string collectionName,
            UpdateCollectionSettings settings, UpdateCollectionOptions? options = null)
        {
            if (settings.History.HasValue &&
                !_bucketConfig.BucketCapabilities.Contains(BucketCapabilities.NON_DEDUPED_HISTORY))
            {
                throw new FeatureNotAvailableException(
                    "UpdateCollectionAsync with History parameter is only supported on Server version 7.2+");
            }

            options ??= UpdateCollectionOptions.Default;
            var (mgmtNode, uri) = GetUri(RestApi.UpdateCollection(_bucketName, scopeName, collectionName));

            using var rootSpan = _tracer.RequestSpan(OuterRequestSpans.ManagerSpan.Collections.UpdateCollection, options.RequestSpanValue)
                .WithCommonTags()
                .WithRemoteAddress(uri);

            var dict = new Dictionary<string, string>();

            if (settings.MaxExpiry.HasValue)
            {
                dict.Add("maxTTL", ((int)settings.MaxExpiry.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture));
            }

            if (settings.History.HasValue)
            {
                dict.Add("history", settings.History.Value.ToLowerString());
            }

            _logger.LogInformation("Attempting to update collection {Collection} - {Uri}", collectionName,
                _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            using var tracker = MetricTracker.Management.StartOperation(
                OuterRequestSpans.ManagerSpan.Collections.UpdateCollection,
                rootSpan,
                _bucketName,
                scopeName,
                collectionName);
            try
            {
                using var httpClient = _httpClientFactory.Create();
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), uri);
                request.Content = new FormUrlEncodedContent(dict);
                requestStopwatch?.Restart();
                using var updateResult = await httpClient.SendAsync(request, cts.FallbackToToken(options.TokenValue))
                    .ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (updateResult.StatusCode != HttpStatusCode.OK)
                {
                    var contentBody = await updateResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ctx = new ManagementErrorContext
                    {
                        HttpStatus = updateResult.StatusCode,
                        Message = contentBody,
                        Statement = uri.ToString()
                    };

                    updateResult.ThrowIfRateLimitingError(contentBody, ctx);

                    if (contentBody.Contains("not found"))
                    {
                        var ex = new CollectionNotFoundException(scopeName, collectionName)
                        {
                            Context = ctx
                        };
                        rootSpan.SetStatus(RequestSpanStatusCode.Error);

                        throw ex;
                    }

                    if (contentBody.Contains("The value must be in range from -1 to 2147483647"))
                    {
                        throw new InvalidArgumentException(contentBody) { Context = ctx };
                    }

                    //Throw any other error cases
                    updateResult.ThrowOnError(ctx);

                    rootSpan.SetStatus(RequestSpanStatusCode.Error);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                rootSpan.SetStatus(RequestSpanStatusCode.Ok);
            }
            catch (Exception exception)
            {
                tracker.SetError(exception);
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                _logger.LogError(exception, "Failed to update collection {Collection} - {Uri}",
                    collectionName, _redactor.SystemData(uri));

                rootSpan.SetStatus(RequestSpanStatusCode.Error);

                throw;
            }
        }
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
