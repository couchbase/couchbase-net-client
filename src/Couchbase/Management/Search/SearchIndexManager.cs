using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Search
{
    internal class SearchIndexManager : ISearchIndexManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SearchIndexManager>();
        private readonly ClusterContext _context;
        private readonly HttpClient _client;

        internal SearchIndexManager(ClusterContext context)
            : this(new CouchbaseHttpClient(context))
        {
            _context = context;
        }

        internal SearchIndexManager(HttpClient httpClient)
        {
            _client = httpClient;
        }

        private Uri GetIndexUri(string indexName = null)
        {
            var node = _context.GetRandomNodeForService(ServiceType.Search);
            var builder = new UriBuilder(node.SearchUri)
            {
                Path = "api/index"
            };

            if (!string.IsNullOrWhiteSpace(indexName))
            {
                builder.Path += $"/{indexName}";
            }

            return builder.Uri;
        }

        private Uri GetQueryControlUri(string indexName, bool allow)
        {
            var baseUri = GetIndexUri(indexName);
            var control = allow ? "allow" : "disallow";

            return new UriBuilder(baseUri)
            {
                Path = $"{baseUri.PathAndQuery}/queryControl/{control}"
            }.Uri;
        }

        private Uri GetFreezeControlUri(string indexName, bool freeze)
        {
            var baseUri = GetIndexUri(indexName);
            var control = freeze ? "freeze" : "unfreeze";

            return new UriBuilder(baseUri)
            {
                Path = $"{baseUri.PathAndQuery}/planFreezeControl/{control}"
            }.Uri;
        }

        private Uri GetIngestControlUri(string indexName, bool pause)
        {
            var baseUri = GetIndexUri(indexName);
            var control = pause ? "pause" : "resume";

            return new UriBuilder(baseUri)
            {
                Path = $"{baseUri.PathAndQuery}/ingestControl/{control}"
            }.Uri;
        }

        private Uri GetIndexedDocumentCountUri(string indexName)
        {
            var baseUri = GetIndexUri(indexName);
            return new UriBuilder(baseUri)
            {
                Path = $"{baseUri.PathAndQuery}/count"
            }.Uri;
        }

        public async Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions options = null)
        {
            options = options ?? AllowQueryingSearchIndexOptions.Default;
            var baseUri = GetQueryControlUri(indexName, true);
            Logger.LogInformation($"Trying to allow querying for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to allow querying for index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions options = null)
        {
            options = options ?? DisallowQueryingSearchIndexOptions.Default;
            var baseUri = GetQueryControlUri(indexName, false);
            Logger.LogInformation($"Trying to disallow querying for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to disallow querying for index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task DropIndexAsync(string indexName, DropSearchIndexOptions options = null)
        {
            options = options ?? DropSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexName);
            Logger.LogInformation($"Trying to drop index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.DeleteAsync(baseUri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to drop index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions options = null)
        {
            options = options ?? FreezePlanSearchIndexOptions.Default;
            var baseUri = GetFreezeControlUri(indexName, true);
            Logger.LogInformation($"Trying to freeze index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to freeze index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions options = null)
        {
            options = options ?? GetAllSearchIndexesOptions.Default;
            var baseUri = GetIndexUri();
            Logger.LogInformation($"Trying to get all indexes - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json["indexDefs"]["indexDefs"].ToObject<Dictionary<string, SearchIndex>>().Values;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to get all indexes - {baseUri}");
                throw;
            }
        }

        public async Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions options = null)
        {
            options = options ?? GetSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexName);
            Logger.LogInformation($"Trying to get index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json["indexDef"].ToObject<SearchIndex>();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to get index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions options = null)
        {
            options = options ?? GetSearchIndexDocumentCountOptions.Default;
            var baseUri = GetIndexedDocumentCountUri(indexName);
            Logger.LogInformation($"Trying to get index document count with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
                return JsonConvert.DeserializeObject<int>(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to get index document count with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions options = null)
        {
            options = options ?? PauseIngestSearchIndexOptions.Default;
            var baseUri = GetIngestControlUri(indexName, true);
            Logger.LogInformation($"Trying to pause ingest for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to pause ingest for index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions options = null)
        {
            options = options ?? ResumeIngestSearchIndexOptions.Default;
            var baseUri = GetIngestControlUri(indexName, false);
            Logger.LogInformation($"Trying to resume ingest for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to resume ingest for index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions options = null)
        {
            options = options ?? UnfreezePlanSearchIndexOptions.Default;
            var baseUri = GetFreezeControlUri(indexName, false);
            Logger.LogInformation($"Trying to unfreeze index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to unfreeze index with name {indexName} - {baseUri}");
                throw;
            }
        }

        public async Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions options = null)
        {
            options = options ?? UpsertSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexDefinition.Name);
            Logger.LogInformation($"Trying to upsert index with name {indexDefinition.Name} - {baseUri}");

            try
            {
                var json = JsonConvert.SerializeObject(indexDefinition, Formatting.None);
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var result = await _client.PutAsync(baseUri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to upsert index with name {indexDefinition.Name} - {baseUri}");
                throw;
            }
        }
    }
}
