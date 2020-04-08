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

#nullable enable

namespace Couchbase.Core.Configuration.Server.Streaming
{
    internal class HttpStreamingConfigListener : IDisposable
    {
        private readonly ILogger<HttpStreamingConfigListener> _logger;
        private readonly ClusterOptions _clusterOptions;
        private readonly HttpClient _httpClient;
        private readonly IConfigHandler _configHandler;
        private readonly string _bucketName;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        private bool _disposed;

        public bool Started { get; private set; }

        public HttpStreamingConfigListener(string bucketName, ClusterOptions clusterOptions, HttpClient httpClient,
            IConfigHandler configHandler, ILogger<HttpStreamingConfigListener> logger)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void StartListening()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpStreamingConfigListener));
            }

            if (Started)
            {
                return;
            }

            Started = true;

            var streamingUriPath = "/pools/default/bs/" + _bucketName;

            Task.Run(async () =>
            {
                _httpClient.Timeout = Timeout.InfiniteTimeSpan;

                var servers = _clusterOptions.ConnectionStringValue!.GetBootstrapEndpoints().ToList().Shuffle();
                while (servers.Any())
                {
                    try
                    {
                        var server = servers.First();
                        servers.Remove(server);

                        var streamingUri = new UriBuilder()
                        {
                            Scheme = _clusterOptions.EffectiveEnableTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                            Host = server.Host,
                            Port = _clusterOptions.BootstrapHttpPort,
                            Path = streamingUriPath
                        };

                        var response = await _httpClient.GetAsync(streamingUri.Uri,
                            HttpCompletionOption.ResponseHeadersRead,
                            _cancellationTokenSource.Token).ConfigureAwait(false);

                        response.EnsureSuccessStatusCode();

                        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        if (stream.CanTimeout)
                        {
                            //the stream itself can timeout if CanTimeout is true on a platform
                            stream.ReadTimeout = Timeout.Infinite;
                        }

                        using var reader = new StreamReader(stream, Encoding.UTF8, false);

                        string? config;
                        while (!_cancellationTokenSource.IsCancellationRequested &&
                               (config = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                        {
                            if (config != string.Empty)
                            {
                                _logger.LogDebug(LoggingEvents.ConfigEvent, config);
                                config = config.Replace("$HOST", server.Host);
                                var bucketConfig = JsonConvert.DeserializeObject<BucketConfig>(config);
                                _configHandler.Publish(bucketConfig);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "HTTP Streaming error.");
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _httpClient?.Dispose();
            }
        }
    }
}
