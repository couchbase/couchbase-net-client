using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Management.Buckets
{
    internal class BucketManager : IBucketManager
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<BucketManager> _logger;
        private readonly IRedactor _redactor;
        private readonly IAppTelemetryCollector _appTelemetryCollector;

        public BucketManager(IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<BucketManager> logger, IRedactor redactor, IAppTelemetryCollector appTelemetryCollector)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _appTelemetryCollector = appTelemetryCollector ?? throw new ArgumentNullException(nameof(appTelemetryCollector));
        }

        private (IClusterNode, Uri) GetUri(string? bucketName = null)
        {
            var mgmtNode = _serviceUriProvider.GetRandomManagementNode();
            var builder = new UriBuilder(mgmtNode.ManagementUri)
            {
                Path = "pools/default/buckets"
            };

            if (!string.IsNullOrWhiteSpace(bucketName))
            {
                builder.Path += $"/{bucketName}";
            }

            return (mgmtNode, builder.Uri);
        }

        public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
        {
            options ??= new CreateBucketOptions();
            var (mgmtNode, uri) = GetUri();

            _logger.LogInformation("Attempting to create bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // create bucket
                var content = new FormUrlEncodedContent(settings!.ToFormValues());
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PostAsync(uri, content, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (result.IsSuccessStatusCode)
                {
                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        mgmtNode.NodesAdapter.CanonicalHostname,
                        mgmtNode.NodesAdapter.AlternateHostname,
                        mgmtNode.NodeUuid,
                        AppTelemetryServiceType.Management,
                        AppTelemetryCounterType.Total);

                    return;
                }

                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var ctx = new ManagementErrorContext
                {
                    HttpStatus  = result.StatusCode,
                    Message = body,
                    Statement = uri.ToString()
                };

                //Throw specific exception if a rate limiting exception is thrown.
                result.ThrowIfRateLimitingError(body, ctx);

                if (result.StatusCode == HttpStatusCode.BadRequest)
                {
                    if (body.IndexOf("Bucket with given name already exists",
                        StringComparison.InvariantCultureIgnoreCase) >= 0)
                    {
                        throw new BucketExistsException(settings.Name);
                    }

                    throw new InvalidArgumentException() { Context = ctx };
                }

                //Throw any other error cases
                result.ThrowOnError(ctx);
            }
            catch (BucketExistsException)
            {
                _logger.LogError("Failed to create bucket with name {settings.Name} because it already exists",
                    _redactor.MetaData(settings.Name));
                throw;
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, "Failed to create bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                throw;
            }
        }

        public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
        {
            options ??= new UpdateBucketOptions();
            var (mgmtNode, uri) = GetUri(settings.Name);
            _logger.LogInformation("Attempting to upsert bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));
            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;

            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                // upsert bucket
                var content = new FormUrlEncodedContent(settings!.ToFormValues());
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PostAsync(uri, content, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (result.IsSuccessStatusCode)
                {
                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        mgmtNode.NodesAdapter.CanonicalHostname,
                        mgmtNode.NodesAdapter.AlternateHostname,
                        mgmtNode.NodeUuid,
                        AppTelemetryServiceType.Management,
                        AppTelemetryCounterType.Total);


                    return;
                }

                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var ctx = new ManagementErrorContext
                {
                    HttpStatus = result.StatusCode,
                    Message = body,
                    Statement = uri.ToString()
                };

                //Throw specific exception if a rate limiting exception is thrown.
                result.ThrowIfRateLimitingError(body, ctx);

                //Throw any other error cases
                result.ThrowOnError(ctx);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, "Failed to upsert bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                throw;
            }
        }

        public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
        {
            options ??= new DropBucketOptions();
            var (mgmtNode, uri) = GetUri(bucketName);
            _logger.LogInformation("Attempting to drop bucket with name {BucketName} - {Uri}", _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;
            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.DeleteAsync(uri, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (result.IsSuccessStatusCode)
                {
                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        mgmtNode.NodesAdapter.CanonicalHostname,
                        mgmtNode.NodesAdapter.AlternateHostname,
                        mgmtNode.NodeUuid,
                        AppTelemetryServiceType.Management,
                        AppTelemetryCounterType.Total);
                    return;
                }

                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogError("Unable to drop bucket with name {bucketName} because it does not exist",
                        _redactor.MetaData(bucketName));
                    throw new BucketNotFoundException(bucketName);
                }

                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var ctx = new ManagementErrorContext
                {
                    HttpStatus = result.StatusCode,
                    Message = body,
                    Statement = uri.ToString()
                };

                //Throw specific exception if a rate limiting exception is thrown.
                result.ThrowIfRateLimitingError(body, ctx);

                //Throw any other error cases
                result.ThrowOnError(ctx);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, "Failed to drop bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                throw;
            }
        }

        public async Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)
        {
            options ??= new FlushBucketOptions();
            var (mgmtNode, uri) = GetUri(bucketName);
            var builder = new UriBuilder(uri);
            builder.Path = Path.Combine(builder.Path, "controller/doFlush");
            uri = builder.Uri;

            _logger.LogInformation("Attempting to flush bucket with name {BucketName} - {Uri}", _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;
            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.PostAsync(uri, null!, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (result.IsSuccessStatusCode)
                {
                    _appTelemetryCollector.IncrementMetrics(
                        operationElapsed,
                        mgmtNode.NodesAdapter.CanonicalHostname,
                        mgmtNode.NodesAdapter.AlternateHostname,
                        mgmtNode.NodeUuid,
                        AppTelemetryServiceType.Management,
                        AppTelemetryCounterType.Total);
                    return;
                }

                var body = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var ctx = new ManagementErrorContext
                {
                    HttpStatus = result.StatusCode,
                    Message = body,
                    Statement = uri.ToString()
                };

                if (result.IsSuccessStatusCode) return;
                switch (result.StatusCode)
                {
                    case HttpStatusCode.NotFound:
                        throw new BucketNotFoundException(bucketName);
                    case HttpStatusCode.BadRequest:
                    {
                        var json = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (json.IndexOf("Flush is disabled for the bucket", StringComparison.InvariantCultureIgnoreCase) >= 0)
                        {
                            throw new BucketIsNotFlushableException(bucketName);
                        }

                        break;
                    }
                }

                //Throw specific exception if a rate limiting exception is thrown.
                result.ThrowIfRateLimitingError(body, ctx);

                //Throw any other error cases
                result.ThrowOnError(ctx);
            }
            catch (BucketNotFoundException)
            {
                _logger.LogError("Unable to flush bucket with name {bucketName} because it does not exist",
                    _redactor.MetaData(bucketName));
                throw;
            }
            catch (BucketIsNotFlushableException)
            {
                _logger.LogError("Failed to flush bucket with name {bucketName} because it is not flushable",
                    _redactor.MetaData(bucketName));
                throw;
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, "Failed to flush bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                throw;
            }
        }

        public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
        {
            options ??= new GetAllBucketsOptions();
            var (mgmtNode, uri) = GetUri();
            _logger.LogInformation("Attempting to get all buckets - {uri}", _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;
            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (!result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                var ctx = new ManagementErrorContext
                {
                    HttpStatus = result.StatusCode,
                    Message = content,
                    Statement = uri.ToString()
                };

                //Throw specific exception if a rate limiting exception is thrown.
                result.ThrowIfRateLimitingError(content, ctx);

                    //Throw any other error cases
                    result.ThrowOnError(ctx);
                }

                using var contentStream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var buckets = await JsonSerializer.DeserializeAsync(contentStream,
                    ManagementSerializerContext.Default.ListBucketSettings,
                    options.TokenValue).ConfigureAwait(false);

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                return buckets!.ToDictionary(
                    p => p.Name,
                    p => p);
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, "Failed to get all buckets - {uri}", _redactor.SystemData(uri));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
                throw;
            }
        }

        public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
        {
            if(bucketName == null) throw new ArgumentNullException(nameof(bucketName));
            options ??= new GetBucketOptions();
            var (mgmtNode, uri) = GetUri(bucketName);
            _logger.LogInformation("Attempting to get bucket with name {bucketName} - {uri}",
                _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            var requestStopwatch = _appTelemetryCollector.StartNewLightweightStopwatch();
            TimeSpan? operationElapsed;
            using var cts = options.TokenValue.FallbackToTimeout(options.TimeoutValue);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                requestStopwatch?.Restart();
                var result = await httpClient.GetAsync(uri, cts.FallbackToToken(options.TokenValue)).ConfigureAwait(false);
                operationElapsed = requestStopwatch?.Elapsed;

                if (!result.IsSuccessStatusCode)
                {
                    var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);

                var ctx = new ManagementErrorContext
                {
                    HttpStatus = result.StatusCode,
                    Message = content,
                    Statement = uri.ToString()
                };

                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new BucketNotFoundException(bucketName)
                    {
                        Context = ctx
                    };
                }

                    //Throw specific exception if a rate limiting exception is thrown.
                    result.ThrowIfRateLimitingError(content, ctx);

                    //Throw any other error cases
                    result.ThrowOnError(ctx);
                }

                _appTelemetryCollector.IncrementMetrics(
                    operationElapsed,
                    mgmtNode.NodesAdapter.CanonicalHostname,
                    mgmtNode.NodesAdapter.AlternateHostname,
                    mgmtNode.NodeUuid,
                    AppTelemetryServiceType.Management,
                    AppTelemetryCounterType.Total);

                using var contentStream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return (await JsonSerializer.DeserializeAsync(contentStream,
                    ManagementSerializerContext.Default.BucketSettings, options.TokenValue).ConfigureAwait(false))!;
            }
            catch (Exception exception)
            {
                operationElapsed = requestStopwatch?.Elapsed;
                _logger.LogError(exception, $"Failed to get bucket with name {bucketName} - {uri}",
                    _redactor.MetaData(bucketName), _redactor.SystemData(uri));
                _appTelemetryCollector.IncrementAppTelemetryErrors(AppTelemetryServiceType.Management, exception, options.TimeoutValue, operationElapsed, mgmtNode.NodesAdapter.CanonicalHostname, mgmtNode.NodesAdapter.AlternateHostname, mgmtNode.NodeUuid);
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
