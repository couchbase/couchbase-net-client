using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server.Streaming
{
    internal class HttpStreamingConfigListener : IDisposable, IAsyncDisposable
    {
        // Has to start non-zero: the ramp below multiplies, and zero times ten stays zero, which is
        // how a total failure came to be retried as fast as the failures came back.
        private const int InitialDelayMs = 100;
        private const int MaxDelayMs = 10000;
        private readonly ILogger<HttpStreamingConfigListener> _logger;
        private readonly ClusterOptions _clusterOptions;
        private readonly ICouchbaseHttpClientFactory _httpClientFactory;
        private readonly IConfigHandler _configHandler;
        private readonly IConfigUpdateEventSink _configSubscriber;
        private readonly string _streamingUriPath;
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private Task? _backgroundTask = null;
        private readonly object _manageTaskLock = new object();

        private bool _disposed;

        public bool Started { get; private set; }

        /// <summary>
        /// Waits out the backoff between rounds. Replaced in tests, which need the duration asked
        /// for: a zero-length wait never reaches a clock, so a <see cref="TimeProvider"/> sees nothing.
        /// </summary>
        internal Func<TimeSpan, CancellationToken, Task> Delay { get; set; } =
            static (duration, cancellationToken) => Task.Delay(duration, cancellationToken);

        public HttpStreamingConfigListener(IConfigUpdateEventSink configSubscriber, ClusterOptions clusterOptions, ICouchbaseHttpClientFactory httpClientFactory,
            IConfigHandler configHandler, ILogger<HttpStreamingConfigListener> logger)
        {
            _configSubscriber = configSubscriber ?? throw new ArgumentNullException(nameof(configSubscriber));
            _streamingUriPath = "/pools/default/bs/" + _configSubscriber.Name;
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
            // Captured once, and used for everything below: Dispose cancels the source and then
            // disposes it, so reading Token inside the loop would throw into the inner catch, which
            // logged an ordinary shutdown as "HTTP Streaming error.". This read can still throw if a
            // Dispose beats the _disposed check above - the same pre-existing window, which used to
            // sit on the Task.Run overload - and StartListening already documents that.
            var listenerToken = _cancellationTokenSource.Token;

            // Ensure that we don't flow the ExecutionContext into the long running task below
            using var flowControl = ExecutionContext.SuppressFlow();

            // Deliberately not given the token, which it had been passed since 2020: that overload
            // only declines to start the delegate, so a Dispose between scheduling and dispatch ends
            // the task Canceled for DisposeAsync to rethrow. The loop checks the token itself.
            return Task.Run(async () =>
            {
                var delayMs = InitialDelayMs;
                while (!listenerToken.IsCancellationRequested)
                {
                    try
                    {
                        var nodes = _configSubscriber?.ClusterNodes.Where(x=>x.HasManagement).ToList().Shuffle();

                        // Per node, not just per round: a shutdown mid-round would otherwise walk every
                        // node left in the list, logging each cancellation as "HTTP Streaming error.".
                        while (nodes != null && nodes.Any() && !listenerToken.IsCancellationRequested)
                        {
                            try
                            {
                                var node = nodes.First();
                                nodes?.Remove(node);

                                _logger.LogDebug("HTTP Streaming with node {node}", node.EndPoint.Host);

                                var streamingUri = new UriBuilder()
                                {
                                    Scheme =
                                        _clusterOptions.EffectiveEnableTls ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                                    Host = node.ManagementUri.Host,
                                    Port = node.ManagementUri.Port,
                                    Path = _streamingUriPath
                                };

                                using var httpClient = _httpClientFactory.Create();
                                httpClient.Timeout = Timeout.InfiniteTimeSpan;

                                var response = await httpClient.GetAsync(streamingUri.Uri,
                                    HttpCompletionOption.ResponseHeadersRead,
                                    listenerToken).ConfigureAwait(false);

                                response.EnsureSuccessStatusCode();

                                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                                if (stream.CanTimeout)
                                {
                                    //the stream itself can timeout if CanTimeout is true on a platform
                                    stream.ReadTimeout = Timeout.Infinite;
                                }

                                using var reader = new StreamReader(stream, Encoding.UTF8, false);

                                string? config;
                                while (!listenerToken.IsCancellationRequested &&
                                       (config = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                                {
                                    if (config != string.Empty)
                                    {
                                        _logger.LogDebug(LoggingEvents.ConfigEvent, config);
                                        config = config.Replace("$HOST", node.EndPoint.Host);
                                        var bucketConfig = JsonSerializer.Deserialize(config,
                                            InternalSerializationContext.Default.BucketConfig)!;
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
                    try
                    {
                        await Delay(TimeSpan.FromMilliseconds(delayMs), listenerToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // Disposed while backing off. Waiting it out would leave this loop alive for
                        // up to ten seconds after the bucket was closed.
                        break;
                    }

                    delayMs = Math.Min(delayMs * 10, MaxDelayMs);
                }
            });
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
