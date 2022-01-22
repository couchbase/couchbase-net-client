using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal class ConfigHandler : IConfigHandler
    {
        private readonly ILogger<ConfigHandler> _logger;

        private BufferBlock<BucketConfig>? _configQueue;
        private ActionBlock<BucketConfig>? _configHandler;

        private readonly ConcurrentDictionary<string, BucketConfig> _configs =
            new ConcurrentDictionary<string, BucketConfig>();

        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly ClusterContext _context;
        private readonly IHttpStreamingConfigListenerFactory _configListenerFactory;

        // Will be set to null when disposed
        private volatile ConcurrentDictionary<string, HttpStreamingConfigListener>? _httpConfigListeners = new();

        private readonly HashSet<IConfigUpdateEventSink> _configChangedSubscribers =
            new HashSet<IConfigUpdateEventSink>();

        private volatile bool _running;
        private volatile bool _disposed;

        public ConfigHandler(ClusterContext context, IHttpStreamingConfigListenerFactory configListenerFactory, ILogger<ConfigHandler> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configListenerFactory = configListenerFactory ?? throw new ArgumentNullException(nameof(configListenerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start(bool withPolling = false)
        {
            if (_running)
            {
                throw new InvalidOperationException($"{nameof(ConfigHandler)} has already been started.");
            }

            _configQueue = new BufferBlock<BucketConfig>(new DataflowBlockOptions
            {
                EnsureOrdered = true,
                CancellationToken = _tokenSource.Token
            });

            _configHandler = new ActionBlock<BucketConfig>(ProcessAsync, new ExecutionDataflowBlockOptions
            {
                BoundedCapacity = 1,
                MaxDegreeOfParallelism = 1,
                EnsureOrdered = true,
                SingleProducerConstrained = true
            });

            _configQueue.LinkTo(_configHandler, new DataflowLinkOptions
            {
                PropagateCompletion = true
            });

            _running = true;

            if (withPolling)
            {
                StartPoll();
            }
        }

        private void StartPoll()
        {
            // We must suppress flow so that the tracing Activity which is current during bootstrap doesn't live on forever
            // as the parent span for all polling activities.
            bool restoreFlow = false;
            try
            {
                if (ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _ = Poll();
            }
            finally
            {
                if (restoreFlow)

                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        private async Task Poll()
        {
            try
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    _logger.LogDebug("Waiting for {interval} before polling.",
                        _context.ClusterOptions.ConfigPollInterval);
                    await Task.Delay(_context.ClusterOptions.ConfigPollInterval, _tokenSource.Token)
                        .ConfigureAwait(false);

                    _logger.LogDebug("Done waiting, polling...");
                    foreach (var clusterNode in _context.Nodes.Where(x =>
                        x.HasKv && x.BucketType != BucketType.Memcached))
                    {
                        _logger.LogDebug("Checking {node} in polling.", clusterNode.EndPoint);
                        try
                        {
                            var config = await clusterNode.GetClusterMap().ConfigureAwait(false);
                            if (config != null)
                            {
                                config.Name ??= "CLUSTER";
                                Publish(config);
                            }
                            else
                            {
                                _logger.LogDebug("Null config for {node} in polling.", clusterNode.EndPoint);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(LoggingEvents.ConfigEvent, e,
                                "Issue getting Cluster Map on server {server}!", clusterNode.EndPoint);
                        }
                    }
                }

                _logger.LogDebug("Broke out of polling loop.");
            }
            catch (Exception e)
            {
                _logger.LogDebug("Polling exception: {e}", e);
            }
        }

        private async Task ProcessAsync(BucketConfig newMap)
        {
            try
            {
                //Set the "effective" network resolution that was resolved at bootstrap time.
                newMap.NetworkResolution = _context.ClusterOptions.EffectiveNetworkResolution;

                _logger.LogDebug(LoggingEvents.ConfigEvent, "Receiving new map revision {revision}", newMap.Rev);
                var isNewOrUpdate = false;
                var stored = _configs.AddOrUpdate(newMap.Name, key =>
                    {
                        _logger.LogDebug(LoggingEvents.ConfigEvent, "Storing new map revision {revision}", newMap.Rev);
                        isNewOrUpdate = true;
                        return newMap;
                    },
                    (key, map) =>
                    {
                        _logger.LogDebug(LoggingEvents.ConfigEvent, "Updating new map revision {revision}", newMap.Rev);
                        if (newMap.Equals(map)) return map;

                        isNewOrUpdate = true;
                        return newMap.Rev > map.Rev ? newMap : map;
                    });

                if (isNewOrUpdate)
                {
                    _logger.LogDebug("Publishing config revision {revision} to subscribers for processing.", stored.Rev);
                    List<IConfigUpdateEventSink> subscribers;
                    lock (_configChangedSubscribers)
                    {
                        subscribers = _configChangedSubscribers.ToList();
                    }

                    var tasks = subscribers.Select(p => p.ConfigUpdatedAsync(stored));
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Error processing new clusterOptions");
            }
        }

        public void Publish(BucketConfig config)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                // Only log if debug logging is enabled to avoid serialization cost
                _logger.LogDebug(LoggingEvents.ConfigEvent, JsonConvert.SerializeObject(config));
            }

            if (_configQueue?.Completion.IsCompleted ?? true)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.");
            }

            if (!_configQueue.Post(config))
            {
                _logger.LogWarning(LoggingEvents.ConfigEvent, "Failed to queue new cluster configuration.");
            }
        }

        public void Subscribe(BucketBase bucket)
        {
            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Add(bucket);
            }

            if (bucket is MemcachedBucket)
            {
                var httpListener = _configListenerFactory.Create(bucket.Name, this);
                if (_httpConfigListeners?.TryAdd(bucket.Name, httpListener) ?? false)
                {
                    httpListener.StartListening();
                }
                else
                {
                    httpListener.Dispose();
                }
            }
        }

        public void Unsubscribe(BucketBase bucket)
        {
            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Remove(bucket);
            }

            if (bucket is MemcachedBucket)
            {
                if (_httpConfigListeners?.TryRemove(bucket.Name, out var listener) ?? false)
                {
                    listener.Dispose();
                }
            }
        }

        public BucketConfig Get(string bucketName)
        {
            try
            {
                if (_configs.TryGetValue(bucketName, out var bucketConfig))
                {
                    return bucketConfig;
                }
            }
            catch (ObjectDisposedException e)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.", e);
            }

            throw new BucketMissingException(@"Cannot find bucket: " + bucketName);
        }

        public void Clear()
        {
            try
            {
                _configs.Clear();
            }
            catch (ObjectDisposedException e)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.", e);
            }
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
