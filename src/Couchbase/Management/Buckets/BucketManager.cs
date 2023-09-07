using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
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

        public BucketManager(IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<BucketManager> logger, IRedactor redactor)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetUri(string? bucketName = null)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = "pools/default/buckets"
            };

            if (!string.IsNullOrWhiteSpace(bucketName))
            {
                builder.Path += $"/{bucketName}";
            }

            return builder.Uri;
        }

        public async Task CreateBucketAsync(BucketSettings settings, CreateBucketOptions? options = null)
        {
            options ??= new CreateBucketOptions();
            var uri = GetUri();

            _logger.LogInformation("Attempting to create bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

            try
            {
                // create bucket
                var content = new FormUrlEncodedContent(settings!.ToFormValues());
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);

                if (result.IsSuccessStatusCode) return;

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
                _logger.LogError(exception, "Failed to create bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpdateBucketAsync(BucketSettings settings, UpdateBucketOptions? options = null)
        {
            options ??= new UpdateBucketOptions();
            var uri = GetUri(settings.Name);
            _logger.LogInformation("Attempting to upsert bucket with name {settings.Name} - {uri}",
                _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

            try
            {
                // upsert bucket
                var content = new FormUrlEncodedContent(settings!.ToFormValues());
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);

                if (result.IsSuccessStatusCode) return;

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
                _logger.LogError(exception, "Failed to upsert bucket with name {settings.Name} - {uri}",
                    _redactor.MetaData(settings.Name), _redactor.SystemData(uri));

                throw;
            }
        }

        public async Task DropBucketAsync(string bucketName, DropBucketOptions? options = null)
        {
            options ??= new DropBucketOptions();
            var uri = GetUri(bucketName);
            _logger.LogInformation("Attempting to drop bucket with name {bucketName} - {uri}",
                    _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                // perform drop
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);

                if (result.IsSuccessStatusCode) return;

                if (result.StatusCode == HttpStatusCode.NotFound)
                {
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
            catch (BucketNotFoundException)
            {
                _logger.LogError("Unable to drop bucket with name {bucketName} because it does not exist",
                    _redactor.MetaData(bucketName));
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task FlushBucketAsync(string bucketName, FlushBucketOptions? options = null)
        {
            options ??= new FlushBucketOptions();
            // get uri and amend path to flush endpoint
            var builder = new UriBuilder(GetUri(bucketName));
            builder.Path = Path.Combine(builder.Path, "controller/doFlush");
            var uri = builder.Uri;

            _logger.LogInformation($"Attempting to flush bucket with name {bucketName} - {uri}",
                _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                // try do flush
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(uri, null!, options.TokenValue).ConfigureAwait(false);

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
                _logger.LogError(exception, "Failed to flush bucket with name {bucketName}",
                    _redactor.MetaData(bucketName));
                throw;
            }
        }

        public async Task<Dictionary<string, BucketSettings>> GetAllBucketsAsync(GetAllBucketsOptions? options = null)
        {
            options ??= new GetAllBucketsOptions();
            var uri = GetUri();
            _logger.LogInformation("Attempting to get all buckets - {uri}", _redactor.SystemData(uri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);

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

                return buckets!.ToDictionary(
                    p => p.Name,
                    p => p);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get all buckets - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<BucketSettings> GetBucketAsync(string bucketName, GetBucketOptions? options = null)
        {
            options ??= new GetBucketOptions();
            var uri = GetUri(bucketName);
            _logger.LogInformation("Attempting to get bucket with name {bucketName} - {uri}",
                _redactor.MetaData(bucketName), _redactor.SystemData(uri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);

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

                using var contentStream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return (await JsonSerializer.DeserializeAsync(contentStream,
                    ManagementSerializerContext.Default.BucketSettings, options.TokenValue).ConfigureAwait(false))!;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, $"Failed to get bucket with name {bucketName} - {uri}",
                    _redactor.MetaData(bucketName), _redactor.SystemData(uri));
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
