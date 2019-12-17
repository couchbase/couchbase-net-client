using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Collections
{
    internal class CollectionManager : ICollectionManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<CollectionManager>();

        private readonly string _bucketName;
        private readonly ClusterContext _context;
        private readonly HttpClient _client;

        public CollectionManager(string bucketName, ClusterContext context, HttpClient client)
        {
            _bucketName = bucketName;
            _context = context;
            _client = client;
        }

        private Uri GetUri(string scopeName = null, string collectionName = null)
        {
            var builder = new UriBuilder(_context.GetRandomNode().ManagementUri)
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

        public async Task<bool> CollectionExistsAsync(CollectionSpec spec, CollectionExistsOptions options = null)
        {
            options = options ?? CollectionExistsOptions.Default;
            var uri = GetUri();
            Logger.LogInformation($"Attempting to verify if scope/collection {spec.ScopeName}/{spec.Name} exists - {uri}");

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);

                // try find scope / collection
                return scopes.Any(scope =>
                    scope.Name == spec.ScopeName && scope.Collections.Any(collection => collection.Name == spec.Name)
                );
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to verify if collection {spec.ScopeName}/{spec.Name} exists - {uri}");
                throw;
            }
        }

        public async Task<bool> ScopeExistsAsync(string scopeName, ScopeExistsOptions options = null)
        {
            options = options ?? ScopeExistsOptions.Default;
            var uri = GetUri();
            Logger.LogInformation($"Attempting to verify if scope {scopeName} exists - {uri}");

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);

                // try find scope
                return scopes.Any(scope => scope.Name == scopeName);
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to verify if scope {scopeName} exists - {uri}");
                throw;
            }
        }

        public async Task<ScopeSpec> GetScopeAsync(string scopeName, GetScopeOptions options = null)
        {
            options = options ?? GetScopeOptions.Default;
            var uri = GetUri();
            Logger.LogInformation($"Attempting to verify if scope {scopeName} exists - {uri}");

            try
            {
                // get all scopes
                var scopes =
                    await GetAllScopesAsync(new GetAllScopesOptions {CancellationToken = options.CancellationToken})
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
                Logger.LogError(exception, $"Failed to verify if scope {scopeName} exists - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<ScopeSpec>> GetAllScopesAsync(GetAllScopesOptions options = null)
        {
            options = options ?? GetAllScopesOptions.Default;
            var uri = GetUri();
            Logger.LogInformation($"Attempting to get all scopes - {uri}");

            try
            {
                // get manifest
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
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
                Logger.LogError(exception, $"Failed to get all scopes - {uri}");
                throw;
            }
        }

        public async Task CreateCollectionAsync(CollectionSpec spec, CreateCollectionOptions options = null)
        {
            options = options ?? CreateCollectionOptions.Default;
            var uri = GetUri(spec.ScopeName);
            Logger.LogInformation($"Attempting create collection {spec.ScopeName}/{spec.Name} - {uri}");

            try
            {
                // check scope exists
                var scopeExists =
                    await ScopeExistsAsync(spec.ScopeName, new ScopeExistsOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);
                if (!scopeExists)
                {
                    throw new ScopeNotFoundException(spec.ScopeName);
                }

                // check collection doesn't exist
                var collectionExists =
                    await CollectionExistsAsync(spec, new CollectionExistsOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);
                if (collectionExists)
                {
                    throw new CollectionExistsException(spec.ScopeName, spec.Name);
                }

                // create collection
                var keys = new Dictionary<string, string>
                {
                    {"name", spec.Name}
                };
                var content = new FormUrlEncodedContent(keys);
                var createResult = await _client.PostAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to create collection {spec.ScopeName}/{spec.Name} - {uri}");
                throw;
            }
        }

        public async Task DropCollectionAsync(CollectionSpec spec, DropCollectionOptions options = null)
        {
            options = options ?? DropCollectionOptions.Default;
            var uri = GetUri(spec.ScopeName, spec.Name);
            Logger.LogInformation($"Attempting drop collection {spec.ScopeName}/{spec.Name} - {uri}");

            try
            {
                // check collection exists
                var collectionExists =
                    await CollectionExistsAsync(spec, new CollectionExistsOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);
                if (!collectionExists)
                {
                    throw new CollectionNotFoundException(spec.ScopeName, spec.Name);
                }

                // drop collection
                var createResult = await _client.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to drop collection {spec.ScopeName}/{spec.Name} - {uri}");
                throw;
            }
        }

        public async Task CreateScopeAsync(ScopeSpec spec, CreateScopeOptions options = null)
        {
            options = options ?? CreateScopeOptions.Default;
            var uri = GetUri();
            Logger.LogInformation($"Attempting create scope {spec.Name} - {uri}");

            try
            {
                // check scope doesn't exists
                var scopeExists =
                    await ScopeExistsAsync(spec.Name, new ScopeExistsOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);
                if (scopeExists)
                {
                    throw new ScopeExistsException(spec.Name);
                }

                // create scope
                var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"name", spec.Name}
                });
                var createResult = await _client.PostAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to create scope {spec.Name} - {uri}");
                throw;
            }
        }

        public async Task DropScopeAsync(string scopeName, DropScopeOptions options = null)
        {
            options = options ?? DropScopeOptions.Default;
            var uri = GetUri(scopeName);
            Logger.LogInformation($"Attempting drop scope {scopeName} - {uri}");

            try
            {
                // check scope exists
                var scopeExists =
                    await ScopeExistsAsync(scopeName, new ScopeExistsOptions {CancellationToken = options.CancellationToken})
                        .ConfigureAwait(false);
                if (!scopeExists)
                {
                    // throw not found
                    throw new ScopeNotFoundException(scopeName);
                }

                // drop scope
                var createResult = await _client.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                createResult.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to drop scope {scopeName} - {uri}");
                throw;
            }
        }
    }
}
