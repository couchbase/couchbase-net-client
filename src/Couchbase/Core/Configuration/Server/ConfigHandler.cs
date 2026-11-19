using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Logging;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal class ConfigHandler : IConfigHandler
    {
        private readonly ILogger<ConfigHandler> _logger;

        // Holds the names of pending configs in arrival order and processes them one at a time.
        private ActionBlock<string>? _configQueue;

        // One config per name until the worker takes it. A newer config replaces it and keeps its place.
        private readonly Dictionary<string, BucketConfig> _pendingConfigs = new();

        private readonly CancellationTokenSource _tokenSource = new();
        private readonly ClusterContext _context;
        private readonly IHttpStreamingConfigListenerFactory _configListenerFactory;

        // Will be set to null when disposed
        private volatile ConcurrentDictionary<string, HttpStreamingConfigListener>? _httpConfigListeners = new();

        private readonly HashSet<IConfigUpdateEventSink> _configChangedSubscribers = new();

        private volatile bool _running;
        private volatile bool _disposed;

        public ConfigHandler(ClusterContext context, IHttpStreamingConfigListenerFactory configListenerFactory, ILogger<ConfigHandler> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configListenerFactory = configListenerFactory ?? throw new ArgumentNullException(nameof(configListenerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            if (_running)
            {
                throw new InvalidOperationException($"{nameof(ConfigHandler)} has already been started.");
            }

            _configQueue = new ActionBlock<string>(name => ProcessAsync(TakePendingConfig(name)),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 1,
                    CancellationToken = _tokenSource.Token
                });

            _running = true;
        }

        public void StartHttpStreaming(string? name = null)
        {
            var listeners = _httpConfigListeners;
            if (listeners is null)
            {
                return;
            }

            foreach (var listener in listeners)
            {
                if ((name is null || listener.Key == name) && !listener.Value.Started)
                {
                    _logger.LogDebug("Starting the HTTP streaming listener of {name}.", listener.Key);
                    listener.Value.StartListening();
                }
            }
        }

        private BucketConfig TakePendingConfig(string name)
        {
            lock (_pendingConfigs)
            {
                var config = _pendingConfigs[name];
                _pendingConfigs.Remove(name);
                return config;
            }
        }

        private async Task ProcessAsync(BucketConfig newMap)
        {
            try
            {
                //Set the "effective" network resolution that was resolved at bootstrap time.
                newMap.NetworkResolution = _context.ClusterOptions.EffectiveNetworkResolution;

                _context.UpdateAppTelemetryConfig(newMap);

                List<IConfigUpdateEventSink> subscribers;
                lock (_configChangedSubscribers)
                {
                    subscribers = _configChangedSubscribers.ToList();
                }

                _logger.LogDebug(LoggingEvents.ConfigEvent, "Receiving new map revision {revision}", newMap.Rev);
                var tasks = subscribers.Select(p => p.ConfigUpdatedAsync(newMap));
                await Task.WhenAll(tasks).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error processing new clusterOptions");
            }
        }

        public void Publish(BucketConfig config)
        {
            //A null config should not make it to the processor
            if(config == null) ThrowHelper.ThrowArgumentNullException(nameof(config));

            if (_logger.IsEnabled(LogLevel.Trace))
            {
                // Only log if debug logging is enabled to avoid serialization cost
                try
                {
                    _logger.LogDebug(LoggingEvents.ConfigEvent,
                        JsonSerializer.Serialize(config, InternalSerializationContext.Default.BucketConfig));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize config");
                }
            }

            var configQueue = _configQueue;
            if (configQueue?.Completion.IsCompleted ?? true)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.");
            }

            // A config with no name is the cluster config.
            var name = config.Name ?? BucketConfig.GlobalBucketName;
            lock (_pendingConfigs)
            {
                if (_pendingConfigs.TryGetValue(name, out var pending))
                {
                    if (config.IsNewerThan(pending))
                    {
                        _pendingConfigs[name] = config;
                    }

                    return;
                }

                _pendingConfigs.Add(name, config);
                if (!configQueue.Post(name))
                {
                    _pendingConfigs.Remove(name);
                    _logger.LogWarning(LoggingEvents.ConfigEvent, "Failed to queue new cluster configuration.");
                }
            }
        }

        public void Subscribe(IConfigUpdateEventSink bucket)
        {
            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Add(bucket);
            }

            var httpListener = _configListenerFactory.Create(bucket, this);
            if (_httpConfigListeners?.TryAdd(bucket.Name, httpListener) ?? false)
            {
                //always add the listener but only start it for memcached
                //for couchbase buckets start if KV config fails
                if (bucket is MemcachedBucket)
                {
                    httpListener.StartListening();
                }
            }
            else
            {
                httpListener.Dispose();
            }
        }

        public void Unsubscribe(IConfigUpdateEventSink configSubscriber)
        {
            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Remove(configSubscriber);
            }

            if (configSubscriber is MemcachedBucket)
            {
                if (_httpConfigListeners?.TryRemove(configSubscriber.Name, out var listener) ?? false)
                {
                    listener.Dispose();
                }
            }
        }

        public BucketConfig Get(string bucketName)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            _logger.LogDebug("Disposing ConfigHandler!");
            if (_disposed)
            {
                return;
            }

            _running = false;
            _disposed = true;

            _tokenSource.Cancel();
            _tokenSource.Dispose();

            var httpConfigListeners = Interlocked.Exchange(ref _httpConfigListeners, null);
            if (httpConfigListeners is not null)
            {
                foreach (var listener in httpConfigListeners.Values)
                {
                    listener.Dispose();
                }
            }

            _configQueue?.Complete();
            _configQueue = null;

            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Clear();
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
