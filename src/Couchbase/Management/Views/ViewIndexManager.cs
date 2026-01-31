using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Management.Views
{
    [Obsolete("The View service has been deprecated use the Query service instead.")]
    internal class ViewIndexManager : IViewIndexManager
    {
        private readonly string _bucketName;
        private readonly IServiceUriProvider _serviceUriProvider;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly ILogger<ViewIndexManager> _logger;
        private readonly IRedactor _redactor;

        public ViewIndexManager(string bucketName, IServiceUriProvider serviceUriProvider, ICouchbaseHttpClientFactory httpClientFactory,
            ILogger<ViewIndexManager> logger, IRedactor redactor)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _serviceUriProvider = serviceUriProvider ?? throw new ArgumentNullException(nameof(serviceUriProvider));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        }

        private Uri GetUri(string? designDocName, DesignDocumentNamespace @namespace)
        {
            // {0}://{1}:{2}/{3}/_design
            var builder = new UriBuilder(_serviceUriProvider.GetRandomViewsUri(_bucketName))
            {
                Path = _bucketName
            };

            if (!string.IsNullOrWhiteSpace(designDocName))
            {
                const string devPrefix = "dev_";
                string name;
                if (@namespace == DesignDocumentNamespace.Production)
                {
                    name = designDocName!.StartsWith(devPrefix) ? designDocName.Substring(devPrefix.Length) : designDocName;
                }
                else
                {
                    name = designDocName!.StartsWith(devPrefix) ? designDocName : string.Concat(devPrefix, designDocName);
                }

                builder.Path += $"/_design/{name}";
            }

            return builder.Uri;
        }

        public async Task<DesignDocument> GetDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, GetDesignDocumentOptions? options = null)
        {
            options ??= GetDesignDocumentOptions.Default;
            var uri = GetUri(designDocName, @namespace);
            _logger.LogInformation("Attempting to get design document {_bucketName}/{designDocName} - {uri}",
                _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));

            try
            {
                using var httpClient = _httpClientFactory.Create();

                var result = await httpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new DesignDocumentNotFoundException(_bucketName, designDocName);
                }

                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var designDocument = JsonConvert.DeserializeObject<DesignDocument>(content);
                designDocument!.Name = designDocName;

                return designDocument;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get design document {_bucketName}/{designDocName} - {uri}",
                    _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task<IEnumerable<DesignDocument>> GetAllDesignDocumentsAsync(DesignDocumentNamespace @namespace, GetAllDesignDocumentsOptions? options = null)
        {
            options ??= GetAllDesignDocumentsOptions.Default;
            var uri = new UriBuilder(_serviceUriProvider.GetRandomManagementUri())
            {
                Path = $"pools/default/buckets/{_bucketName}/ddocs"
            }.Uri;
            _logger.LogInformation("Attempting to get all design documents for bucket {_bucketName} - {uri}",
                _redactor.MetaData(_bucketName), _redactor.SystemData(uri));

            try
            {
                using var httpClient = _httpClientFactory.Create();

                var result = await httpClient.GetAsync(uri, options.TokenValue).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(content);

                var designDocuments = new List<DesignDocument>();
                if (json.TryGetValue("rows", out var rows))
                {
                    foreach (var row in rows)
                    {
                        var designDoc = new DesignDocument
                        {
                            Name = row.SelectToken("doc.meta.id")!.Value<string>()!.Replace("_design/", string.Empty)
                        };

                        foreach (var view in row.SelectTokens("doc.json.views"))
                        {
                            var name = view.First!.Path.Substring(view.First.Path.LastIndexOf(".", StringComparison.Ordinal) + 1);
                            var map = view.First!.First!.SelectToken("map");
                            var reduce = view.First.First.SelectToken("reduce");
                            designDoc.Views.Add(name, new View
                            {
                                Map = map?.Value<string>(),
                                Reduce = reduce?.Value<string>()
                            });
                        }

                        designDocuments.Add(designDoc);
                    }
                }

                return designDocuments;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to get all design documents for bucket {_bucketName} - {uri}",
                    _redactor.MetaData(_bucketName), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task UpsertDesignDocumentAsync(DesignDocument designDocument, DesignDocumentNamespace @namespace, UpsertDesignDocumentOptions? options = null)
        {
            options ??= UpsertDesignDocumentOptions.Default;
            var json = JsonConvert.SerializeObject(designDocument);
            var uri = GetUri(designDocument.Name, @namespace);
            _logger.LogInformation("Attempting to upsert design document {_bucketName}/{designDocument.Name} - {uri}",
                _redactor.MetaData(_bucketName), _redactor.MetaData(designDocument.Name), _redactor.SystemData(uri));
            _logger.LogDebug(json);

            try
            {
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to upsert design document {_bucketName}/{designDocument.Name} - {uri} - {json}",
                    _redactor.MetaData(_bucketName), _redactor.MetaData(designDocument.Name), _redactor.SystemData(uri), _redactor.MetaData(json));
                throw;
            }
        }

        public async Task DropDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, DropDesignDocumentOptions? options = null)
        {
            options ??= DropDesignDocumentOptions.Default;
            var uri = GetUri(designDocName, @namespace);
            _logger.LogInformation("Attempting to drop design document {_bucketName}/{designDocName} - {uri}",
                _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));

            try
            {
                using var httpClient = _httpClientFactory.Create();
                var result = await httpClient.DeleteAsync(uri, options.TokenValue).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogError("Failed to drop design document {_bucketName}/{designDocName} because it does not exist - {uri}",
                        _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));
                    throw new DesignDocumentNotFoundException(_bucketName, designDocName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to drop design document {_bucketName}/{designDocName} - {uri}",
                    _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));
                throw;
            }
        }

        public async Task PublishDesignDocumentAsync(string designDocName, PublishDesignDocumentOptions? options = null)
        {
            options ??= PublishDesignDocumentOptions.Default;
            var uri = GetUri(designDocName, DesignDocumentNamespace.Production);
            _logger.LogInformation("Attempting to publish design document {_bucketName}/{designDocName} - {uri}",
            _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));

            try
            {
                // get dev design document
                var designDocument = await GetDesignDocumentAsync(designDocName, DesignDocumentNamespace.Development, GetDesignDocumentOptions.Default).ConfigureAwait(false);
                var json = JsonConvert.SerializeObject(designDocument);

                // publish design doc to production
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                using var httpClient = _httpClientFactory.Create();
                var publishResult = await httpClient.PutAsync(uri, content, options.TokenValue).ConfigureAwait(false);
                publishResult.EnsureSuccessStatusCode();
            }
            catch (DesignDocumentNotFoundException)
            {
                _logger.LogError("Failed to publish design document {_bucketName}/{designDocName} because it does not exist",
                    _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName));
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to put design document {_bucketName}/{designDocName} - {uri}",
                    _redactor.MetaData(_bucketName), _redactor.MetaData(designDocName), _redactor.SystemData(uri));
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
