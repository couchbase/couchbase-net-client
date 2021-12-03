using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Exceptions;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Management.Collections
{
    /// <remarks>Volatile</remarks>
    internal class CollectionManager : ICouchbaseCollectionManager
    {
        private readonly string _bucketName;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<CollectionManager> _logger;
        private readonly IRedactor _redactor;

        /// <summary>
        /// REST endpoint path definitions.
        /// </summary>
        public static class RestApi
        {
            public static string GetScope(string bucketName, string scopeName) => $"pools/default/buckets/{bucketName}/scopes/{scopeName}";
            public static string GetScopes(string bucketName) => $"pools/default/buckets/{bucketName}/scopes";
            public static string CreateScope(string bucketName) => $"pools/default/buckets/{bucketName}/scopes";
            public static string DeleteScope(string bucketName, string scopeName) => $"pools/default/buckets/{bucketName}/scopes/{scopeName}";
            public static string CreateCollections(string bucketName, string scopeName) => $"pools/default/buckets/{bucketName}/scopes/{scopeName}/collections";
            public static string DeleteCollections(string bucketName, string scopeName, string collectionName) => $"pools/default/buckets/{bucketName}/scopes/{scopeName}/collections/{collectionName}";
        }

        public CollectionManager(string bucketName, IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<CollectionManager> logger, IRedactor redactor)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        internal Uri GetUri(string path)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = path
            };

            return builder.Uri;
        }

        public async Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions? options = null)
        {
            options ??= CollectionExistsOptions.Default;
            var uri = GetUri(RestApi.GetScope(_bucketName, spec.ScopeName));
            _logger.LogInformation(
                "Attempting to verify if scope/collection {spec.ScopeName}/{spec.Name} exists - {uri}", spec.ScopeName,
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
                _logger.LogError(exception, "Failed to verify if collection {spec.ScopeName}/{spec.Name} exists - {uri}", spec.ScopeName,
                    spec.Name, _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<bool> ScopeExistsAsync(string scopeName, ScopeExistsOptions? options = null)
        {
            options ??= ScopeExistsOptions.Default;
            var uri = GetUri(RestApi.GetScope(_bucketName, scopeName));
            _logger.LogInformation("Attempting to verify if scope {scopeName} exists - {uri}", scopeName,
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
                _logger.LogError(exception, "Failed to verify if scope {scopeName} exists - {uri}", scopeName,
                    _redactor.SystemData(uri));
                throw;
            }
        }

        [Obsolete("Use GetAllScopesAsync instead.")]
        public async Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
        {
            options ??= GetScopeOptions.Default;
            var uri = GetUri(RestApi.GetScope(_bucketName, scopeName));
            _logger.LogInformation("Attempting to verify if scope {scopeName} exists - {uri}", scopeName,
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
                _logger.LogError(exception, "Failed to verify if scope {scopeName} exists - {uri}",scopeName,
                _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions? options = null)
        {
            options ??= GetAllScopesOptions.Default;
            var uri = GetUri(RestApi.GetScopes(_bucketName));
            _logger.LogInformation("Attempting to get all scopes - {uri}", _redactor.SystemData(uri));

            try
            {
                // get manifest
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);

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

                // check scope & collection exists in manifest
                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                var scopes = json.SelectToken("scopes");

                return scopes.Select(scope => new ScopeSpec(scope["name"].Value<string>())
                {
                    Collections = scope["collections"].Select(collection =>
                        new CollectionSpec(scope["name"].Value<string>(), collection["name"].Value<string>())
                        {
                            MaxExpiry = collection["maxTTL"] == null
                                ? (TimeSpan?) null
                                : TimeSpan.FromSeconds(collection["maxTTL"].Value<long>())
                        }
                    ).ToList()
                }).ToList();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get all scopes - {uri}", _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions? options = null)
        {
            options ??= CreateCollectionOptions.Default;
            var uri = GetUri(RestApi.CreateCollections(_bucketName, spec.ScopeName));
            _logger.LogInformation("Attempting create collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            try
            {
                // create collection
                var keys = new Dictionary<string, string>
                {
                    {"name", spec.Name}
                };

                if (spec.MaxExpiry.HasValue)
                {
                    keys.Add("maxTTL", spec.MaxExpiry.Value.TotalSeconds.ToString(CultureInfo.InvariantCulture));
                }
                var content = new FormUrlEncodedContent(keys!);
                using var httpClient = _httpClientFactory.Create();
                var createResult = await httpClient.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);

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
                        throw new CollectionExistsException(spec.ScopeName, spec.Name)
                        {
                            Context = ctx
                        };

                    if (contentBody.Contains("not found"))
                        throw ScopeNotFoundException.FromScopeName(spec.ScopeName);

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                    spec.Name, _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions? options = null)
        {
            options ??= DropCollectionOptions.Default;
            var uri = GetUri(RestApi.DeleteCollections(_bucketName, spec.ScopeName, spec.Name));
            _logger.LogInformation("Attempting drop collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            try
            {
                // drop collection
                using var httpClient = _httpClientFactory.Create();
                var createResult = await httpClient.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
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
                _logger.LogError(exception, "Failed to drop collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
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
            var uri = GetUri(RestApi.CreateScope(_bucketName));
            _logger.LogInformation("Attempting create scope {spec.Name} - {uri}", scopeName, _redactor.SystemData(uri));

            // create scope
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"name", scopeName}
            }!);

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var createResult = await httpClient.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);
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
                        throw new ScopeExistsException(scopeName)
                    {
                        Context = ctx
                    };

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create scope {spec.Name} - {uri}", scopeName,
                    _redactor.SystemData(uri));
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
            var uri = GetUri(RestApi.DeleteScope(_bucketName, scopeName));
            _logger.LogInformation("Attempting drop scope {scopeName} - {uri}", scopeName, _redactor.SystemData(uri));

            try
            {
                // drop scope
                using var httpClient = _httpClientFactory.Create();
                var createResult = await httpClient.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
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
                        throw new ScopeNotFoundException(scopeName)
                        {
                            Context = ctx
                        };

                    //Throw any other error cases
                    createResult.ThrowOnError(ctx);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop scope {scopeName} - {uri}", scopeName, _redactor.SystemData(uri));
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
