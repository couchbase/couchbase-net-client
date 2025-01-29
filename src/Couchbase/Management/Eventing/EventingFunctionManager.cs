#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.Operations;
using Couchbase.Management.Eventing.Internal;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using CollectionNotFoundException = Couchbase.Management.Collections.CollectionNotFoundException;

namespace Couchbase.Management.Eventing
{
    internal class EventingFunctionManager : IEventingFunctionManager
    {
        private readonly IEventingFunctionService _service;
        private readonly ILogger<EventingFunctionManager> _logger;
        private readonly IRequestTracer _tracer;
        private readonly EventingFunctionKeyspace? _managementScope;
        private readonly IServiceUriProvider _serviceUriProvider;
        private IAppTelemetryCollector _appTelemetryCollector;

        public EventingFunctionManager(IEventingFunctionService service, ILogger<EventingFunctionManager> logger, IRequestTracer tracer, IServiceUriProvider serviceUriProvider, IAppTelemetryCollector appTelemetryCollector, EventingFunctionKeyspace? managementScope = null)
        {
            _service = service;
            _logger = logger;
            _tracer = tracer;
            _managementScope = managementScope;
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _appTelemetryCollector = appTelemetryCollector ?? throw new ArgumentNullException(nameof(appTelemetryCollector));
        }

        private CancellationTokenPairSource CreateRetryTimeoutCancellationTokenSource(FunctionOptionsBase options) =>
            CancellationTokenPairSource.FromTimeout(options.Timeout, options.Token);

        internal (IClusterNode, Uri) GetUri(string path)
        {
            var eventingNode = _serviceUriProvider.GetRandomEventingNode();
            var builder = new UriBuilder(eventingNode.EventingUri)
            {
                Path = path
            };
            var isGlobal = _managementScope is null or { Bucket: "*", Scope: "*" };
            if (!isGlobal)
            {
                builder.Query =
                    $"bucket={Uri.EscapeDataString(_managementScope!.Bucket)}&scope={Uri.EscapeDataString(_managementScope!.Scope)}";
            }

            return (eventingNode, builder.Uri);
        }

        /// <inheritdoc />
        public async Task UpsertFunctionAsync(EventingFunction function, UpsertFunctionOptions? options = null)
        {
            //POST http://localhost:8096/api/v1/functions/<name>;
            options ??= UpsertFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(function.Name)}";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.UpsertFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.PostAsync(uri, rootSpan, encodeSpan, tokenPair.Token, function,
                        managementScope: _managementScope).ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_HANDLER_COMPILATION":
                                throw new EventingFunctionCompilationFailureException(errorResponse.GetDescription());
                            case "ERR_COLLECTION_MISSING":
                                throw new CollectionNotFoundException(errorResponse.GetDescription());
                            case "ERR_SRC_MB_SAME":
                                throw new EventingFunctionIdenticalKeyspaceException(errorResponse.GetDescription());
                            case "ERR_BUCKET_MISSING":
                                throw new BucketNotFoundException(errorResponse.GetDescription());
                            case "ERR_INVALID_CONFIG":
                                throw new InvalidArgumentException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while upserting event function {function.Name}.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DropFunctionAsync(string name, DropFunctionOptions? options = null)
        {
            //DELETE http://localhost:8096/api/v1/functions/<name>
            options ??= DropFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.DropFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.DeleteAsync(uri, rootSpan, encodeSpan, tokenPair.Token)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_DEPLOYED":
                                throw new EventingFunctionNotDeployedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_UNDEPLOYED":
                                throw new EventingFunctionDeployedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, "An error occurred while dropping eventing function '{function}'.", Uri.EscapeDataString(name));
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IEnumerable<EventingFunction>> GetAllFunctionsAsync(GetAllFunctionOptions? options = null)
        {
            //GET http://localhost:8096/api/v1/functions
            options ??= GetAllFunctionOptions.Default;

            const string path = "/api/v1/functions";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.GetAllFunctions, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.GetAsync(uri, rootSpan, encodeSpan, tokenPair.Token)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    response.EnsureSuccessStatusCode();

                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        eventingNode.NodesAdapter.CanonicalHostname,
                        eventingNode.NodesAdapter.AlternateHostname,
                        eventingNode.NodeUuid,
                        AppTelemetryServiceType.Eventing,
                        AppTelemetryCounterType.Total);

                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    var allFunctions = await JsonSerializer.DeserializeAsync(stream, EventingSerializerContext.Primary.EventingFunctionList)
                        .ConfigureAwait(false);
                    if (allFunctions is null)
                    {
                        return Enumerable.Empty<EventingFunction>();
                    }

                    var filteredFunctions = allFunctions.Where(eventingFunction =>
                    {
                        if (_managementScope is null &&
                            eventingFunction.FunctionScope is null or { Bucket: "*", Scope: "*" })
                        {
                            return true;
                        }

                        if (_managementScope is not null &&
                            eventingFunction.FunctionScope.Bucket == _managementScope.Bucket
                            && eventingFunction.FunctionScope.Scope == _managementScope.Scope)
                        {
                            return true;
                        }

                        return false;
                    });
                    return filteredFunctions;
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, "An error occurred while getting all eventing functions.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<EventingFunction?> GetFunctionAsync(string name, GetFunctionOptions? options = null)
        {
            //GET http://localhost:8096/api/v1/functions/<name>
            options ??= GetFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.GetFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                   using var response = await _service.GetAsync(uri, rootSpan, encodeSpan, tokenPair.Token)
                        .ConfigureAwait(false);
                   operationElapsed = requestStopwatch?.Elapsed;

                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);

                        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        var deserialized = await JsonSerializer.DeserializeAsync(stream, EventingSerializerContext.Primary.EventingFunction)
                            .ConfigureAwait(false);
                        return deserialized ?? throw new CouchbaseException($"Could not parse response for {nameof(GetFunctionAsync)})");
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_BOOTSTRAPPED":
                                throw new EventingFunctionNotBootstrappedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_DEPLOYED":
                                throw new EventingFunctionNotDeployedException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);

                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while getting event function {name}.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task PauseFunctionAsync(string name, PauseFunctionOptions? options = null)
        {
            //POST http://localhost:8096/api/v1/functions/<name>/pause
            options ??= PauseFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}/pause";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.PauseFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.PostAsync(uri, rootSpan, encodeSpan, tokenPair.Token, managementScope: _managementScope)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_BOOTSTRAPPED":
                                throw new EventingFunctionNotBootstrappedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_DEPLOYED":
                                throw new EventingFunctionNotDeployedException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while pausing eventing function '{name}'.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task ResumeFunctionAsync(string name, ResumeFunctionOptions? options = null)
        {
            //POST http://localhost:8096/api/v1/functions/<name>/resume
            options ??= ResumeFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}/resume";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.ResumeFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.PostAsync(uri, rootSpan, encodeSpan, tokenPair.Token, managementScope: _managementScope)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_DEPLOYED":
                                throw new EventingFunctionNotDeployedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while resume eventing function '{name}'.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task DeployFunctionAsync(string name, DeployFunctionOptions? options = null)
        {
            //POST http://localhost:8096/api/v1/functions/<name>/deploy
            options ??= DeployFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}/deploy";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.DeployFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.PostAsync(uri, rootSpan, encodeSpan, tokenPair.Token, managementScope: _managementScope)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_BOOTSTRAPPED":
                                throw new EventingFunctionNotBootstrappedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while pausing eventing function '{name}'.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task UndeployFunctionAsync(string name, UndeployFunctionOptions? options = null)
        {
            //POST http://localhost:8096/api/v1/functions/<name>/undeploy
            options ??= UndeployFunctionOptions.Default;

            var path = $"/api/v1/functions/{Uri.EscapeDataString(name)}/undeploy";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.UndeployFunction, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.PostAsync(uri, rootSpan, encodeSpan, tokenPair.Token, managementScope: _managementScope)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    if (response.IsSuccessStatusCode)
                    {
                        _appTelemetryCollector.IncrementMetrics(
                            operationElapsed,
                            eventingNode.NodesAdapter.CanonicalHostname,
                            eventingNode.NodesAdapter.AlternateHostname,
                            eventingNode.NodeUuid,
                            AppTelemetryServiceType.Eventing,
                            AppTelemetryCounterType.Total);
                        return;
                    }

                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (content.TryDeserialize(EventingSerializerContext.Primary.ErrorResponse, out var errorResponse))
                    {
                        switch (errorResponse.Name)
                        {
                            case "ERR_APP_NOT_DEPLOYED":
                                throw new EventingFunctionNotDeployedException(errorResponse.GetDescription());
                            case "ERR_APP_NOT_FOUND_TS":
                                throw new EventingFunctionNotFoundException(errorResponse.GetDescription());
                        }
                    }

                    //throw any non-specific errors
                    throw new CouchbaseException(content);
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, $"An error occurred while un-deploying eventing function '{name}'.");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<EventingStatus> FunctionsStatus(FunctionsStatusOptions? options = null)
        {
            //GET http://localhost:8096/api/v1/status
            options ??= FunctionsStatusOptions.Default;

            var path = $"/api/v1/status";
            var (eventingNode, uri) = GetUri(path);
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            try
            {
                using var rootSpan = RootSpan(OuterRequestSpans.ManagerSpan.Eventing.FunctionsStatus, options)
                    .WithLocalAddress()
                    .WithStatement(path)
                    .WithCommonTags();

                using var encodeSpan = rootSpan.DispatchSpan(options);
                using (var tokenPair = CreateRetryTimeoutCancellationTokenSource(options))
                {
                    requestStopwatch?.Restart();
                    using var response = await _service.GetAsync(uri, rootSpan, encodeSpan, tokenPair.Token)
                        .ConfigureAwait(false);
                    operationElapsed = requestStopwatch?.Elapsed;
                    response.EnsureSuccessStatusCode();

                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        eventingNode.NodesAdapter.CanonicalHostname,
                        eventingNode.NodesAdapter.AlternateHostname,
                        eventingNode.NodeUuid,
                        AppTelemetryServiceType.Eventing,
                        AppTelemetryCounterType.Total);

                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    var deserialized = await JsonSerializer.DeserializeAsync(stream, EventingSerializerContext.Primary.EventingStatus)
                        .ConfigureAwait(false);
                    return deserialized ?? new EventingStatus();
                }
            }
            catch (Exception e)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Eventing, e, options.Timeout, operationElapsed, eventingNode.NodesAdapter.CanonicalHostname, eventingNode.NodesAdapter.AlternateHostname, eventingNode.NodeUuid);
                _logger.LogError(e, "An error occurred while getting the eventing function status.");
                throw;
            }
        }

        #region tracing

        private IRequestSpan RootSpan(string operation, FunctionOptionsBase options)
        {
            var span = _tracer.RequestSpan(operation, options.RequestSpan);
            if (span.CanWrite)
            {
                span.SetAttribute(OuterRequestSpans.Attributes.System.Key, OuterRequestSpans.Attributes.System.Value);
                span.SetAttribute(OuterRequestSpans.Attributes.Service, OuterRequestSpans.ServiceSpan.Eventing);
                span.SetAttribute(OuterRequestSpans.Attributes.Operation, operation);
            }

            return span;
        }

        #endregion
    }
}
