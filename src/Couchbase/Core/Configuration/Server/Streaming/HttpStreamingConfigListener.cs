using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

#nullable enable

namespace Couchbase.Core.Configuration.Server.Streaming
{
    internal class HttpStreamingConfigListener : IDisposable, IAsyncDisposable
    {
        private const int InitialDelayMs = 0;
        private const int MaxDelayMs = 10000;
        private readonly ILogger<HttpStreamingConfigListener> _logger;
        private readonly ClusterOptions _clusterOptions;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly IConfigHandler _configHandler;
        private readonly string _bucketName;
        private readonly string _streamingUriPath;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _backgroundTask = null;
        private readonly object _manageTaskLock = new object();

        private bool _disposed;

        public bool Started { get; private set; }

        public HttpStreamingConfigListener(string bucketName, ClusterOptions clusterOptions, ICouchbaseHttpClientFactory httpClientFactory,
            IConfigHandler configHandler, ILogger<HttpStreamingConfigListener> logger)
        {
            _bucketName = bucketName ?? throw new ArgumentNullException(nameof(bucketName));
            _streamingUriPath = "/pools/default/bs/" + _bucketName;
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _configHandler = configHandler ?? throw new ArgumentNullException(nameof(configHandler));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void StartListening()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(HttpStreamingConfigListener));
            }

            if (Started && _backgroundTask?.Status.HasFlag(TaskStatus.Running) == true)
            {
                return;
            }

            Started = true;

            lock (_manageTaskLock)
            {
                if (_backgroundTask?.Status.HasFlag(TaskStatus.Running) != true)
                {
                    _backgroundTask = StartBackgroundTask();
                }
            }
        }

        private Task StartBackgroundTask()
        {
            // Ensure that we don't flow the ExecutionContext into the long running task below
            using var flowControl = ExecutionContext.SuppressFlow();

            return Task.Run(async () =>
            {
                var delayMs = InitialDelayMs;

                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    try
                    {

                        var servers = _clusterOptions.ConnectionStringValue?.GetBootstrapEndpoints().ToList().Shuffle();

                        while (servers != null && servers.Any())
                        {
                            try
                            {
                                var server = servers.First();
                                servers?.Remove(server);

                                var streamingUri = new UriBuilder()
                                {
                                    Scheme =
                                        _clusterOptions.EffectiveEnableTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                                    Host = server.Host,
                                    Port = _clusterOptions.BootstrapHttpPort,
                                    Path = _streamingUriPath
                                };

                                using var httpClient = _httpClientFactory.Create();
                                httpClient.Timeout = Timeout.InfiniteTimeSpan;

                                var response = await httpClient.GetAsync(streamingUri.Uri,
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

                                    // on success, reset the exponential delay
                                    delayMs = InitialDelayMs;
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, "HTTP Streaming error.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "HTTP Streaming error. (outer loop)");
                    }

                    // if we exited the inner loop, then all servers failed and we need to start over.
                    // however, we don't want to create a failstorm in the logs if the failure is 100%
                    // try again, but with an exponential delay of up to 10s.
                    await Task.Delay(delayMs).ConfigureAwait(false);
                    delayMs = Math.Min(delayMs * 10, MaxDelayMs);
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
            }
        }

        public async ValueTask DisposeAsync()
        {
            Dispose();
            if (_backgroundTask != null)
            {
                await _backgroundTask.ConfigureAwait(false);
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
