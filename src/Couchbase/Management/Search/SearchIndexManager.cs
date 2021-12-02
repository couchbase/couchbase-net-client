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

#nullable enable

namespace Couchbase.Management.Search
{
    internal class SearchIndexManager : ISearchIndexManager
    {
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<SearchIndexManager> _logger;
        private readonly IRedactor _redactor;

        public SearchIndexManager(IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<SearchIndexManager> logger, IRedactor redactor)
        {
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetIndexUri(string? indexName = null)
        {
            var searchUri = _serviceUriProvider.GetRandomSearchUri();
            var builder = new UriBuilder(searchUri)
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

        public async Task AllowQueryingAsync(string indexName, AllowQueryingSearchIndexOptions? options = null)
        {
            options ??= AllowQueryingSearchIndexOptions.Default;
            var baseUri = GetQueryControlUri(indexName, true);
            _logger.LogInformation("Trying to allow querying for index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to allow querying for index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task DisallowQueryingAsync(string indexName, DisallowQueryingSearchIndexOptions? options = null)
        {
            options ??= DisallowQueryingSearchIndexOptions.Default;
            var baseUri = GetQueryControlUri(indexName, false);
            _logger.LogInformation("Trying to disallow querying for index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to disallow querying for index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task DropIndexAsync(string indexName, DropSearchIndexOptions? options = null)
        {
            options ??= DropSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexName);
            _logger.LogInformation("Trying to drop index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.DeleteAsync(baseUri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task FreezePlanAsync(string indexName, FreezePlanSearchIndexOptions? options = null)
        {
            options ??= FreezePlanSearchIndexOptions.Default;
            var baseUri = GetFreezeControlUri(indexName, true);
            _logger.LogInformation("Trying to freeze index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to freeze index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task<IEnumerable<SearchIndex>> GetAllIndexesAsync(GetAllSearchIndexesOptions? options = null)
        {
            options ??= GetAllSearchIndexesOptions.Default;
            var baseUri = GetIndexUri();
            _logger.LogInformation("Trying to get all indexes - {baseUri}", _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json["indexDefs"]["indexDefs"].ToObject<Dictionary<string, SearchIndex>>().Values;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get all indexes - {baseUri}", _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task<SearchIndex> GetIndexAsync(string indexName, GetSearchIndexOptions? options = null)
        {
            options ??= GetSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexName);
            _logger.LogInformation("Trying to get index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var json = JObject.Parse(await result.Content.ReadAsStringAsync().ConfigureAwait(false));
                return json["indexDef"].ToObject<SearchIndex>();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task<int> GetIndexedDocumentsCountAsync(string indexName, GetSearchIndexDocumentCountOptions? options = null)
        {
            options ??= GetSearchIndexDocumentCountOptions.Default;
            var baseUri = GetIndexedDocumentCountUri(indexName);
            _logger.LogInformation("Trying to get index document count with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.GetAsync(baseUri,  options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
                var responseBody = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var jobj = JObject.Parse(responseBody);
                return jobj["count"].Value<int>();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get index document count with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task PauseIngestAsync(string indexName, PauseIngestSearchIndexOptions? options = null)
        {
            options ??= PauseIngestSearchIndexOptions.Default;
            var baseUri = GetIngestControlUri(indexName, true);
            _logger.LogInformation("Trying to pause ingest for index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to pause ingest for index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task ResumeIngestAsync(string indexName, ResumeIngestSearchIndexOptions? options = null)
        {
            options ??= ResumeIngestSearchIndexOptions.Default;
            var baseUri = GetIngestControlUri(indexName, false);
            _logger.LogInformation("Trying to resume ingest for index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to resume ingest for index with name {indexName} - {baseUri}",
                    _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task UnfreezePlanAsync(string indexName, UnfreezePlanSearchIndexOptions? options = null)
        {
            options ??= UnfreezePlanSearchIndexOptions.Default;
            var baseUri = GetFreezeControlUri(indexName, false);
            _logger.LogInformation("Trying to unfreeze index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PostAsync(baseUri, null!, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new SearchIndexNotFound(indexName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to unfreeze index with name {indexName} - {baseUri}",
                _redactor.MetaData(indexName), _redactor.SystemData(baseUri));
                throw;
            }
        }

        public async Task UpsertIndexAsync(SearchIndex indexDefinition, UpsertSearchIndexOptions? options = null)
        {
            options ??= UpsertSearchIndexOptions.Default;
            var baseUri = GetIndexUri(indexDefinition.Name);
            _logger.LogInformation("Trying to upsert index with name {indexDefinition.Name} - {baseUri}",
                _redactor.MetaData(indexDefinition.Name), _redactor.SystemData(baseUri));

            try
            {
                var json = JsonConvert.SerializeObject(indexDefinition, Formatting.None);
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PutAsync(baseUri, content, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to upsert index with name {indexDefinition.Name} - {baseUri}",
                    _redactor.MetaData(indexDefinition.Name), _redactor.SystemData(baseUri));
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
