using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.IO.HTTP;

namespace Couchbase.Core.Configuration.Server
{
    public class BucketConfigEventArgs : EventArgs
    {
        public BucketConfigEventArgs(BucketConfig config)
        {
            Config = config;
        }

        public BucketConfig Config { get; }
    }

    internal class ConfigHandler : IConfigHandler
    {
        private static readonly ILogger Log = LogManager.CreateLogger<ConfigHandler>();

        private readonly BlockingCollection<BucketConfig> _configQueue =
            new BlockingCollection<BucketConfig>(new ConcurrentQueue<BucketConfig>());

        private readonly ConcurrentDictionary<string, BucketConfig> _configs =
            new ConcurrentDictionary<string, BucketConfig>();

        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        private readonly ClusterContext _context;

        private readonly ConcurrentDictionary<string, HttpStreamingConfigListener> _httpConfigListeners =
            new ConcurrentDictionary<string, HttpStreamingConfigListener>();

        private readonly CouchbaseHttpClient _httpClient;

        internal delegate void BucketConfigHandler(object sender, BucketConfigEventArgs a);

        public event BucketConfigHandler ConfigChanged;

        public ConfigHandler(ClusterContext context, CouchbaseHttpClient httpClient)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public void Start(CancellationTokenSource tokenSource)
        {
            TokenSource = tokenSource;
            Task.Run(Process, TokenSource.Token);
        }

        public void Stop()
        {
            TokenSource.Cancel();
            TokenSource.Dispose();
        }

        public void Poll(CancellationToken token = default(CancellationToken))
        {
            Task.Run(async () =>
            {
                Thread.CurrentThread.Name = "cnfg";
                while (!TokenSource.IsCancellationRequested)
                {
                    await Task.Delay(_context.ClusterOptions.ConfigPollInterval, TokenSource.Token).ConfigureAwait(false);

                    foreach (var clusterNode in _context.Nodes)
                    {
                        try
                        {
                            var config = await clusterNode.Value.GetClusterMap().ConfigureAwait(false);
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
                            Log.LogWarning(e, "Issue getting Cluster Map cluster!");
                        }
                    }
                }
            }, token);
        }

        public void Process()
        {
            foreach (var newMap in _configQueue.GetConsumingEnumerable())
            {
                try
                {
                    var isNewOrUpdate = false;
                    var stored = _configs.AddOrUpdate(newMap.Name, key =>
                        {
                            isNewOrUpdate = true;
                            return newMap;
                        },
                        (key, map) =>
                        {
                            if (newMap.Equals(map)) return map;

                            isNewOrUpdate = true;
                            return newMap.Rev > map.Rev ? newMap : map;
                        });

                    if (isNewOrUpdate)
                    {
                        ConfigChanged?.Invoke(this, new BucketConfigEventArgs(stored));
                    }
                }
                catch (Exception e)
                {
                    Log.LogWarning(e, "Error processing new clusterOptions");
                }
            }
        }

        public void Publish(BucketConfig config)
        {
            try
            {
                _configQueue.Add(config);
            }
            catch (ObjectDisposedException e)
            {
                throw new ContextStoppedException("ConfigHandler is in stopped mode.", e);
            }
        }

        public void Subscribe(BucketBase bucket)
        {
            ConfigChanged += bucket.ConfigUpdated;

            if (bucket is MemcachedBucket)
            {
                var httpListener = new HttpStreamingConfigListener(bucket.Name, _context.ClusterOptions, _httpClient, this, TokenSource.Token);
                if (_httpConfigListeners.TryAdd(bucket.Name, httpListener))
                {
                    httpListener.StartListening();
                }
            }
        }

        public void Unsubscribe(BucketBase bucket)
        {
            ConfigChanged -= bucket.ConfigUpdated;

            if (bucket is MemcachedBucket)
            {
                if(_httpConfigListeners.TryRemove(bucket.Name, out HttpStreamingConfigListener listener))
                {
                    listener.Dispose();
                }
            }
        }

        public BucketConfig Get(string bucketName)
        {
            try
            {
                if (_configs.TryGetValue(bucketName, out BucketConfig bucketConfig))
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
            _configQueue?.Dispose();
            TokenSource?.Dispose();
            if (ConfigChanged == null) return;
            foreach (var subscriber in ConfigChanged.GetInvocationList())
            {
                ConfigChanged -= (BucketConfigHandler) subscriber;
            }
        }
    }
}
