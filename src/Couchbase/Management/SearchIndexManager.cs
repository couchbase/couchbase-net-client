using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management
{
    internal class SearchIndexManager : ISearchIndexManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<SearchIndexManager>();
        private readonly ClusterOptions _clusterOptions;
        private readonly HttpClient _client;

        internal SearchIndexManager(ClusterOptions clusterOptions)
            : this (clusterOptions, new CouchbaseHttpClient(clusterOptions))
        { }

        internal SearchIndexManager(ClusterOptions clusterOptions, HttpClient httpClient)
        {
            _clusterOptions = clusterOptions;
            _client = httpClient;
        }

        private Uri GetIndexUri(string indexName = null)
        {
            var node = _clusterOptions.GlobalNodes.GetRandom(x => x.HasSearch());

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

        public async Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions options)
        {
            var baseUri = GetQueryControlUri(indexName, true);
            Logger.LogInformation($"Trying to allow querying for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions options)
        {
            var baseUri = GetQueryControlUri(indexName, false);
            Logger.LogInformation($"Trying to disallow querying for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task DropIndexAsync(string indexName, DropSearchIndexOptions options)
        {
            var baseUri = GetIndexUri(indexName);
            Logger.LogInformation($"Trying to drop index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.DeleteAsync(baseUri, options.CancellationToken).ConfigureAwait(false);
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

        public async Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions options)
        {
            var baseUri = GetFreezeControlUri(indexName, true);
            Logger.LogInformation($"Trying to freeze index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions options)
        {
            var baseUri = GetIndexUri();
            Logger.LogInformation($"Trying to get all indexes - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.CancellationToken).ConfigureAwait(false);
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

        public async Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions options)
        {
            var baseUri = GetIndexUri(indexName);
            Logger.LogInformation($"Trying to get index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.CancellationToken).ConfigureAwait(false);
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

        public async Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions options)
        {
            var baseUri = GetIndexedDocumentCountUri(indexName);
            Logger.LogInformation($"Trying to get index document count with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.GetAsync(baseUri,  options.CancellationToken).ConfigureAwait(false);
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

        public async Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions options)
        {
            var baseUri = GetIngestControlUri(indexName, true);
            Logger.LogInformation($"Trying to pause ingest for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions options)
        {
            var baseUri = GetIngestControlUri(indexName, false);
            Logger.LogInformation($"Trying to resume ingest for index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions options)
        {
            var baseUri = GetFreezeControlUri(indexName, false);
            Logger.LogInformation($"Trying to unfreeze index with name {indexName} - {baseUri}");

            try
            {
                var result = await _client.PostAsync(baseUri, null, options.CancellationToken).ConfigureAwait(false);
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

        public async Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions options)
        {
            var baseUri = GetIndexUri(indexDefinition.Name);
            Logger.LogInformation($"Trying to upsert index with name {indexDefinition.Name} - {baseUri}");

            try
            {
                var json = JsonConvert.SerializeObject(indexDefinition, Formatting.None);
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var result = await _client.PutAsync(baseUri, content, options.CancellationToken).ConfigureAwait(false);
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
