using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
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

    internal class ConfigContext : IConfigContext
    {
        private static readonly ILogger Logger = LogManager.CreateLogger<ConfigContext>();

        private readonly BlockingCollection<BucketConfig> _configQueue = new BlockingCollection<BucketConfig>(new ConcurrentQueue<BucketConfig>());
        private readonly ConcurrentDictionary<string, BucketConfig> _configs = new ConcurrentDictionary<string, BucketConfig>();
        public CancellationTokenSource TokenSource { get; set; } = new CancellationTokenSource();
        private readonly Couchbase.Configuration _configuration;
        private readonly ConcurrentDictionary<string, HttpStreamingConfigListener> _httpConfigListeners = new ConcurrentDictionary<string, HttpStreamingConfigListener>();
        private readonly HttpClient _httpClient;

        internal delegate void BucketConfigHandler(object sender, BucketConfigEventArgs a);

        public event BucketConfigHandler ConfigChanged;

        public ConfigContext(Couchbase.Configuration configuration)
        {
            _configuration = configuration;
            _httpClient = new CouchbaseHttpClient(_configuration);
        }

        public void Start(CancellationTokenSource tokenSource)
        {
            TokenSource = tokenSource;
            Task.Run(() => Process(), TokenSource.Token);
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
                    await Task.Delay(_configuration.ConfigPollInterval, TokenSource.Token).ConfigureAwait(false);

                    foreach (var clusterNode in _configuration.GlobalNodes.Where(x=>x.Connection != null))
                    {
                        try
                        {
                            var config = await clusterNode.GetClusterMap().ConfigureAwait(false);
                            Publish(config);
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning(e, "Issue getting Cluster Map config!");
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
                    var isUpdate = false;
                    var stored = _configs.AddOrUpdate(newMap.Name, newMap, (key, oldMap) =>
                    {
                        if (newMap.Equals(oldMap))
                        {
                            return oldMap;
                        }

                        isUpdate = true;
                        return newMap.Rev > oldMap.Rev ? newMap : oldMap;
                    });

                    if (isUpdate)
                    {
                        if (stored.Rev > newMap.Rev)
                        {
                            ConfigChanged?.Invoke(newMap, new BucketConfigEventArgs(stored));
                        }
                    }
                    else
                    {
                        ConfigChanged?.Invoke(newMap, new BucketConfigEventArgs(stored));
                    }
                }
                catch (Exception e)
                {
                    Logger.LogWarning(e, "Error processing new configuration");
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
                throw new ContextStoppedException("ConfigContext is in stopped mode.", e);
            }
        }

        public void Subscribe(BucketBase bucket)
        {
            ConfigChanged += bucket.ConfigUpdated;

            if (bucket is MemcachedBucket)
            {
                var httpListener = new HttpStreamingConfigListener(bucket.Name, _configuration, _httpClient, this, TokenSource.Token);
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
                throw new ContextStoppedException("ConfigContext is in stopped mode.", e);
            }

            throw new BucketMissingException(@"Cannot find bucket: {bucketName}");
        }

        public void Clear()
        {
            try
            {
                _configs.Clear();
            }
            catch (ObjectDisposedException e)
            {
                throw new ContextStoppedException("ConfigContext is in stopped mode.", e);
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
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
