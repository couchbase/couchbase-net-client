using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Couchbase.Core.Configuration.Server.Streaming
{
    internal class HttpStreamingConfigListener : IDisposable
    {
        private static readonly ILogger Log = LogManager.CreateLogger<HttpStreamingConfigListener>();
        private readonly ClusterOptions _clusterOptions;
        private readonly HttpClient _httpClient;
        private CancellationToken _cancellationToken;
        private readonly ConfigHandler _couchbaseContext;
        private readonly string _bucketName;

        public HttpStreamingConfigListener(string bucketName, ClusterOptions clusterOptions, HttpClient httpClient,
            ConfigHandler couchbaseContext, CancellationToken cancellationToken)
        {
            _bucketName = bucketName;
            _clusterOptions = clusterOptions;
            _httpClient = httpClient;
            _couchbaseContext = couchbaseContext;
            _cancellationToken = cancellationToken;
        }

        public void StartListening()
        {
            var streamingUriPath = "/pools/default/bs/" + _bucketName;

            Task.Run(async () =>
            {
                using (_httpClient)
                {
                    _httpClient.Timeout = Timeout.InfiniteTimeSpan;

                    var servers = _clusterOptions.Servers.ToList().Shuffle();
                    while (servers.Any())
                    {
                        try
                        {
                            var server = servers.First();
                            servers.Remove(server);

                            var streamingUri = new UriBuilder(server)
                            {
                                Scheme = _clusterOptions.UseSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                                Port = _clusterOptions.MgmtPort,
                                Path = streamingUriPath
                            };

                            var response = await _httpClient.GetAsync(streamingUri.Uri,
                                HttpCompletionOption.ResponseHeadersRead,
                                _cancellationToken).ConfigureAwait(false);

                            response.EnsureSuccessStatusCode();

                            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                            {
                                _cancellationToken.Register(Dispose);

                                if (stream.CanTimeout)
                                {
                                    //the stream itself can timeout if CanTimeout is true on a platform
                                    stream.ReadTimeout = Timeout.Infinite;
                                }

                                using (var reader = new StreamReader(stream, Encoding.UTF8, false))
                                {
                                    string config;
                                    while (!_cancellationToken.IsCancellationRequested &&
                                           (config = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                                    {
                                        if (config != string.Empty)
                                        {
                                            config = config.Replace("$HOST", server.Host);
                                            var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(config);
                                            _couchbaseContext.Publish(bucketConfig);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.LogError(e, "HTTP Streaming error.");
                        }
                    }
                }
            }, _cancellationToken);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
