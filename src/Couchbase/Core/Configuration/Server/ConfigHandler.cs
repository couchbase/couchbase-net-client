using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly BlockingCollection<BucketConfig> _configQueue =
            new BlockingCollection<BucketConfig>(new ConcurrentQueue<BucketConfig>());

        private readonly ConcurrentDictionary<string, BucketConfig> _configs =
            new ConcurrentDictionary<string, BucketConfig>();

        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly ClusterContext _context;
        private readonly IHttpStreamingConfigListenerFactory _configListenerFactory;

        private readonly ConcurrentDictionary<string, HttpStreamingConfigListener> _httpConfigListeners =
            new ConcurrentDictionary<string, HttpStreamingConfigListener>();

        private readonly HashSet<IConfigUpdateEventSink> _configChangedSubscribers =
            new HashSet<IConfigUpdateEventSink>();

        private Thread? _thread;
        private bool _disposed;

        public ConfigHandler(ClusterContext context, IHttpStreamingConfigListenerFactory configListenerFactory, ILogger<ConfigHandler> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _configListenerFactory = configListenerFactory ?? throw new ArgumentNullException(nameof(configListenerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start(bool withPolling = false)
        {
            if (_thread != null)
            {
                throw new InvalidOperationException($"{nameof(ConfigHandler)} has already been started.");
            }

            _thread = new Thread(Process)
            {
                IsBackground = true,
                Name = nameof(ConfigHandler)
            };
            _thread.Start();

            if (withPolling)
            {
                Poll();
            }
        }

        private void Poll()
        {
            Task.Run(async () =>
            {
                while (!_tokenSource.IsCancellationRequested)
                {
                    await Task.Delay(_context.ClusterOptions.ConfigPollInterval, _tokenSource.Token).ConfigureAwait(false);

                    foreach (var clusterNode in _context.Nodes.Where(x=>x.HasKv && x.BucketType != BucketType.Memcached))
                    {
                        try
                        {
                            var config = await clusterNode.GetClusterMap().ConfigureAwait(false);
                            if (config != null)
                            {
                                if (config.Name == null)
                                {
                                    config.Name = "CLUSTER";
                                }
                                Publish(config);
                            }
                        }
                        catch (Exception e)
                        {
                            _logger.LogWarning(e, "Issue getting Cluster Map cluster!");
                        }
                    }
                }
            }, _tokenSource.Token);
        }

        private void Process()
        {
            try
            {
                // Don't use cancellation token for GetConsumingEnumerable, causes some strange exceptions
                // The call to _configQueue.CompleteAdding will exit the loop instead
                foreach (var newMap in _configQueue.GetConsumingEnumerable())
                {
                    try
                    {
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
                            Task.WhenAll(tasks).GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.LogWarning(e, "Error processing new clusterOptions");
                    }

                    if (_tokenSource.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // There is a problem in older versions of SemaphoreSlim used by BlockingCollection than can
                // cause an NRE if GetConsumingEnumerable is still enumerating when BlockingCollection is
                // disposed. We need to eat that error to prevent crashes. https://github.com/dotnet/coreclr/pull/24776
                _logger.LogDebug(ex, "Ignoring unhandled exception in ConfigHandler.");
            }
        }

        public void Publish(BucketConfig config)
        {
            try
            {
                _logger.LogDebug(LoggingEvents.ConfigEvent, JsonConvert.SerializeObject(config));
                _configQueue.Add(config);
            }
            catch (ObjectDisposedException e)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.", e);
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
                if (_httpConfigListeners.TryAdd(bucket.Name, httpListener))
                {
                    httpListener.StartListening();

                    // Dispose the listener when we're stopped
                    _tokenSource.Token.Register(state =>
                    {
                        ((HttpStreamingConfigListener) state!).Dispose();
                    }, httpListener);
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
                if(_httpConfigListeners.TryRemove(bucket.Name, out var listener))
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
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _configQueue.CompleteAdding();
            _configQueue.Dispose();

            _tokenSource.Cancel();
            _tokenSource.Dispose();

            lock (_configChangedSubscribers)
            {
                _configChangedSubscribers.Clear();
            }
        }
    }
}
