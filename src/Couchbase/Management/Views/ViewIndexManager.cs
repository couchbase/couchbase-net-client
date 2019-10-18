using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Services;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Couchbase.Management.Views
{
    internal class ViewIndexManager : IViewIndexManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<ViewIndexManager>();

        private readonly string _bucketName;
        private readonly HttpClient _client;
        private readonly ClusterContext _context;

        internal ViewIndexManager(string bucketName, HttpClient client, ClusterContext context)
        {
            _bucketName = bucketName;
            _client = client;
            _context = context;
        }

        private Uri GetUri(string designDocName, DesignDocumentNamespace @namespace)
        {
            // {0}://{1}:{2}/{3}/_design
            var builder = new UriBuilder(_context.GetRandomNodeForService(ServiceType.KeyValue, _bucketName).ViewsUri)
            {
                Path = _bucketName
            };

            if (!string.IsNullOrWhiteSpace(designDocName))
            {
                const string devPrefix = "dev_";
                string name;
                if (@namespace == DesignDocumentNamespace.Production)
                {
                    name = designDocName.StartsWith(devPrefix) ? designDocName.Substring(devPrefix.Length) : designDocName;
                }
                else
                {
                    name = designDocName.StartsWith(devPrefix) ? designDocName : string.Concat(devPrefix, designDocName);
                }

                builder.Path += $"/_design/{name}";
            }

            return builder.Uri;
        }

        public async Task<DesignDocument> GetDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, GetDesignDocumentOptions options)
        {
            var uri = GetUri(designDocName, @namespace);
            Logger.LogInformation($"Attempting to get design document {_bucketName}/{designDocName} - {uri}");

            try
            {
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new DesignDocumentNotFoundException(_bucketName, designDocName);
                }

                result.EnsureSuccessStatusCode();

                var content = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                var designDocument = JsonConvert.DeserializeObject<DesignDocument>(content);
                designDocument.Name = designDocName;

                return designDocument;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to get design document {_bucketName}/{designDocName} - {uri}");
                throw;
            }
        }

        public async Task<IEnumerable<DesignDocument>> GetAllDesignDocumentsAsync(DesignDocumentNamespace @namespace, GetAllDesignDocumentsOptions options)
        {
            var uri = new UriBuilder(_context.GetRandomNodeForService(ServiceType.KeyValue, _bucketName).ViewsUri)
            {
                Port = _context.ClusterOptions.MgmtPort,
                Path = $"pools/default/buckets/{_bucketName}/ddocs"
            }.Uri;
            Logger.LogInformation($"Attempting to get all design documents for bucket {_bucketName} - {uri}");

            try
            {
                var result = await _client.GetAsync(uri, options.CancellationToken).ConfigureAwait(false);
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
                            Name = row.SelectToken("doc.meta.id").Value<string>().Replace("_design/", string.Empty)
                        };

                        foreach (var view in row.SelectTokens("doc.json.views"))
                        {
                            var name = view.First.Path.Substring(view.First.Path.LastIndexOf(".") + 1);
                            var map = view.First.First.SelectToken("map");
                            var reduce = view.First.First.SelectToken("reduce");
                            designDoc.Views.Add(name, new View
                            {
                                Map = map.Value<string>(),
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
                Logger.LogError(exception, $"Failed to get all design documents for bucket {_bucketName} - {uri}");
                throw;
            }
        }

        public async Task UpsertDesignDocumentAsync(DesignDocument designDocument, DesignDocumentNamespace @namespace, UpsertDesignDocumentOptions options)
        {
            var json = JsonConvert.SerializeObject(designDocument);
            var uri = GetUri(designDocument.Name, @namespace);
            Logger.LogInformation($"Attempting to upsert design document {_bucketName}/{designDocument.Name} - {uri}");
            Logger.LogDebug(json);

            try
            {
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var result = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to upsert design document {_bucketName}/{designDocument.Name} - {uri} - {json}");
                throw;
            }
        }

        public async Task DropDesignDocumentAsync(string designDocName, DesignDocumentNamespace @namespace, DropDesignDocumentOptions options)
        {
            var uri = GetUri(designDocName, @namespace);
            Logger.LogInformation($"Attempting to drop design document {_bucketName}/{designDocName} - {uri}");

            try
            {
                var result = await _client.DeleteAsync(uri, options.CancellationToken).ConfigureAwait(false);
                if (result.StatusCode == HttpStatusCode.NotFound)
                {
                    Logger.LogError($"Failed to drop design document {_bucketName}/{designDocName} because it does not exist - {uri}");
                    throw new DesignDocumentNotFound(_bucketName, designDocName);
                }

                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to drop design document {_bucketName}/{designDocName} - {uri}");
                throw;
            }
        }

        public async Task PublishDesignDocumentAsync(string designDocName, PublishDesignDocumentOptions options)
        {
            var uri = GetUri(designDocName, DesignDocumentNamespace.Production);
            Logger.LogInformation($"Attempting to publish design document {_bucketName}/{designDocName} - {uri}");

            try
            {
                // get dev design document
                var designDocument = await GetDesignDocumentAsync(designDocName, DesignDocumentNamespace.Development, GetDesignDocumentOptions.Default).ConfigureAwait(false);
                var json = JsonConvert.SerializeObject(designDocument);

                // publish design doc to production
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var publishResult = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                publishResult.EnsureSuccessStatusCode();
            }
            catch (DesignDocumentNotFoundException)
            {
                Logger.LogError($"Failed to publish design document {_bucketName}/{designDocName} because it does not exist");
                throw;
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to put design document {_bucketName}/{designDocName} - {uri}");
                throw;
            }
        }
    }
}
