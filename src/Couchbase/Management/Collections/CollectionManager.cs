using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Management.Collections
{
    /// <remarks>Volatile</remarks>
    internal class CollectionManager : ICouchbaseCollectionManager
    {
        private readonly string _bucketName;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly CouchbaseHttpClient _client;
        private readonly ILogger<CollectionManager> _logger;
        private readonly IRedactor _redactor;

        public CollectionManager(string bucketName, IServiceUriProvider serviceUriProvider, CouchbaseHttpClient client,
            ILogger<CollectionManager> logger, IRedactor redactor)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetUri(string? scopeName = null, string? collectionName = null)
        {
            var builder = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = $"pools/default/buckets/{_bucketName}/collections"
            };

            if (!string.IsNullOrWhiteSpace(scopeName))
            {
                builder.Path += $"/{scopeName}";
            }

            if (!string.IsNullOrWhiteSpace(collectionName))
            {
                builder.Path += $"/{collectionName}";
            }

            return builder.Uri;
        }

        public async Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions? options = null)
        {
            options ??= CollectionExistsOptions.Default;
            var uri = GetUri();
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
            var uri = GetUri();
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

        public async Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions? options = null)
        {
            options ??= GetScopeOptions.Default;
            var uri = GetUri();
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
                throw new ScopeNotFoundException(scopeName);
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
            var uri = GetUri();
            _logger.LogInformation("Attempting to get all scopes - {uri}", _redactor.SystemData(uri));

            try
            {
                // get manifest
                var result = await _client.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                // check scope & collection exists in manifest
                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                var scopes = json.SelectToken("scopes");

                if (scopes.Type != JTokenType.Array) // TODO: remove after SDK beta as per RFC
                {
                    // older style - scopes is a map
                    return scopes.Select(scope =>
                    {
                        var scopeName = scope.Path.Substring(scope.Path.LastIndexOf(".", StringComparison.InvariantCulture) + 1);
                        var collections = scope.First()["collections"].Select(collection =>
                        {
                            var collectionName = collection.Path.Substring(collection.Path.LastIndexOf(".", StringComparison.InvariantCulture) + 1);
                            return new CollectionSpec(scopeName, collectionName);
                        }).ToList();

                        return new ScopeSpec(scopeName)
                        {
                            Collections = collections
                        };
                    }).ToList();
                }

                // newer style - scopes is an array
                return scopes.Select(scope => new ScopeSpec(scope["name"].Value<string>())
                {
                    Collections = scope["collections"].Select(collection =>
                        new CollectionSpec(collection["name"].Value<string>(), scope["name"].Value<string>())
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
            var uri = GetUri(spec.ScopeName);
            _logger.LogInformation("Attempting create collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            try
            {
                // create collection
                var keys = new Dictionary<string, string>
                {
                    {"name", spec.Name}
                };
                var content = new FormUrlEncodedContent(keys);
                var createResult = await _client.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
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
            var uri = GetUri(spec.ScopeName, spec.Name);
            _logger.LogInformation("Attempting drop collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                spec.Name, _redactor.SystemData(uri));

            try
            {
                // drop collection
                var createResult = await _client.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop collection {spec.ScopeName}/{spec.Name} - {uri}", spec.ScopeName,
                    spec.Name, _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions? options = null)
        {
            options ??= CreateScopeOptions.Default;
            var uri = GetUri();
            _logger.LogInformation("Attempting create scope {spec.Name} - {uri}", spec.Name, _redactor.SystemData(uri));

            try
            {
                // create scope
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"name", spec.Name}
                });
                var createResult = await _client.PostAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to create scope {spec.Name} - {uri}", spec.Name,
                    _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task DropScopeAsync(string scopeName, DropScopeOptions? options = null)
        {
            options ??= DropScopeOptions.Default;
            var uri = GetUri(scopeName);
            _logger.LogInformation("Attempting drop scope {scopeName} - {uri}", scopeName, _redactor.SystemData(uri));

            try
            {
                // drop scope
                var createResult = await _client.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop scope {scopeName} - {uri}", scopeName, _redactor.SystemData(uri));
                throw;
            }
        }
    }
}
