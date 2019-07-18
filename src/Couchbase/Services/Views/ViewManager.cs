using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Couchbase.Services.Views
{
    internal class ViewManager : IViewManager
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<ViewManager>();

        private readonly string _bucketName;
        private readonly HttpClient _client;
        private readonly Configuration _configuration;
        private readonly string _scheme;
        private readonly int _port;

        internal ViewManager(string bucketName, HttpClient client, Configuration configuration)
        {
            _bucketName = bucketName;
            _client = client;
            _configuration = configuration;

            //TODO: use configured ports
            if (_configuration.UseSsl)
            {
                _scheme = "https";
                _port = 18092;
            }
            else
            {
                _scheme = "http";
                _port = 8092;
            }
        }

        private Uri GetUri(string designDocName, bool isProduction)
        {
            // TODO: should only get node with KV service enabled
            var server = _configuration.Servers.GetRandom();

            // {0}://{1}:{2}/{3}/_design
            var builder = new UriBuilder
            {
                Scheme = _scheme,
                Host = server.Host,
                Port = _port,
                Path = _bucketName
            };

            if (!string.IsNullOrWhiteSpace(designDocName))
            {
                const string devPrefix = "dev_";
                string name;
                if (isProduction)
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

        public async Task<DesignDocument> GetAsync(string designDocName, GetViewIndexOptions options)
        {
            var uri = GetUri(designDocName, options.IsProduction);
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

        public async Task<IEnumerable<DesignDocument>> GetAllAsync(GetAllViewIndexOptions options)
        {
            var uri = new UriBuilder
            {
                Scheme = _scheme,
                Host = _configuration.Servers.GetRandom().Host,
                Port = 8091,
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

        public async Task CreateAsync(DesignDocument designDocument, CreateViewIndexOptions options)
        {
            var json = JsonConvert.SerializeObject(designDocument);
            var uri = GetUri(designDocument.Name, options.IsProduction);
            Logger.LogInformation($"Attempting to create design document {_bucketName}/{designDocument.Name} - {uri}");
            Logger.LogDebug(json);

            try
            {
                try
                {
                    // Check design document doesn't already exist - will throw if not
                    await GetAsync(designDocument.Name, new GetViewIndexOptions { IsProduction = options.IsProduction}).ConfigureAwait(false);
                    throw new DesignDocumentAlreadyExistsException(_bucketName, designDocument.Name);
                }
                catch (DesignDocumentNotFoundException)
                {
                    // we expect this exception
                }

                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var result = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                result.EnsureSuccessStatusCode();
            }
            catch (Exception exception)
            {
                Logger.LogError(exception, $"Failed to create design document {_bucketName}/{designDocument.Name} - {uri} - {json}");
                throw;
            }
        }

        public async Task UpsertAsync(DesignDocument designDocument, UpsertViewIndexOptions options)
        {
            var json = JsonConvert.SerializeObject(designDocument);
            var uri = GetUri(designDocument.Name, options.IsProduction);
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

        public async Task DropAsync(string designDocName, DropViewIndexOptions options)
        {
            var uri = GetUri(designDocName, options.IsProduction);
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

        public async Task PublishAsync(string designDocName, PublishIndexOptions options)
        {
            var uri = GetUri(designDocName, true);
            Logger.LogInformation($"Attempting to publish design document {_bucketName}/{designDocName} - {uri}");

            try
            {
                // get dev design document
                var designDocument = await GetAsync(designDocName, GetViewIndexOptions.Default).ConfigureAwait(false);
                var json = JsonConvert.SerializeObject(designDocument);

                // publish design doc to production
                var content = new StringContent(json, Encoding.UTF8, MediaType.Json);
                var publishResult = await _client.PutAsync(uri, content, options.CancellationToken).ConfigureAwait(false);
                publishResult.EnsureSuccessStatusCode();

                // drop old dev design doc
                await DropAsync(designDocName, DropViewIndexOptions.Default);
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
