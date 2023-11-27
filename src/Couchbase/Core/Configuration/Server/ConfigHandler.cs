using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal class ConfigHandler : IConfigHandler
    {
        private readonly ILogger<ConfigHandler> _logger;
        private BufferBlock<BucketConfig>? _configQueue;
        private ActionBlock<BucketConfig>? _configHandler;
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
                if (!ExecutionContext.IsFlowSuppressed())
                {
                    ExecutionContext.SuppressFlow();
                    restoreFlow = true;
                }

                _ = PollAsync();
            }
            finally
            {
                if (restoreFlow)
                {
                    ExecutionContext.RestoreFlow();
                }
            }
        }

        private async Task PollAsync()
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

                    bool connected = false;
                    foreach (var clusterNode in _context.Nodes.Where(x =>
                        x.HasKv && x.BucketType != BucketType.Memcached))
                    {
                        //Skip the polling process on a node if Faster Fail-over is enabled
                        //the SDK will use logic to determine if the config has changed and
                        //directly fetch a new one from the server and enqueue it.
                        if (clusterNode.ServerFeatures.ClustermapChangeNotificationBrief)
                        {
                            continue;
                        }

                        _logger.LogDebug("Checking {node} in polling.", clusterNode.EndPoint);
                        try
                        {
                            var config = await clusterNode.GetClusterMap().ConfigureAwait(false);
                            if (config != null)
                            {
                                connected = true;
                                config.Name ??= BucketConfig.GlobalBucketName;
                                Publish(config);
                            }
                            else
                            {
                                _logger.LogDebug("Null config for {node} in polling.", clusterNode.EndPoint);
                            }
                        }
                        catch (DocumentNotFoundException)
                        {
                            //If this happens were a mixed node cluster and need to break
                            //out of the loop and switch to HTTP streaming for cluster configs.
                           throw;
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(LoggingEvents.ConfigEvent, e,
                                "Issue getting Cluster Map on server {server}!", clusterNode.EndPoint);
                        }
                    }

                    if (!connected && _context!.ClusterOptions!.ConnectionStringValue!.IsDnsSrv)
                    {
                        _logger.LogInformation("Bootstrapping: The handler can no longer connect " +
                            " to the cluster and will attempt to rebootstrap against the DNS SRV records.");

                        //If we reach here, we cannot connect to the cluster via CCCP
                        //and we know we are using DNS SRV lookup. Since, the bucket
                        //may have moved to a different cluster so we want to refresh
                        //the seed list by doing a DNS SRV lookup and then continuing
                        //to cycle threw the new nodes list.
                        await _context.BootstrapGlobalAsync().ConfigureAwait(false);
                        await _context.RebootstrapAllBuckets().ConfigureAwait(false);
                    }
                }

                _logger.LogDebug("Broke out of polling loop.");
            }
            catch (DocumentNotFoundException)
            {
                _logger.LogDebug("Switching to HTTP streaming for config handling.");

                //Were in a mixed node cluster so switch to HttpStreaming
                var httpListeners = _httpConfigListeners?.Values;

                _logger.LogDebug("Getting the HTTP streaming listener ready.");
                if (httpListeners != null)
                {
                    foreach (var httpListener in httpListeners)
                    {
                        if (!httpListener.Started)
                        {
                            _logger.LogDebug("Starting the HTTP streaming listener.");
                            httpListener.StartListening();
                            _logger.LogDebug("Started the HTTP streaming listener.");
                        }
                    }
                }
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

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                // Only log if debug logging is enabled to avoid serialization cost
                _logger.LogDebug(LoggingEvents.ConfigEvent,
                    JsonSerializer.Serialize(config, InternalSerializationContext.Default.BucketConfig));
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
