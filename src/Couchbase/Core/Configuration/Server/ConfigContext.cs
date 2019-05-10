using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Utils;

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

    internal class ConfigContext : IDisposable
    {
        private readonly BlockingCollection<BucketConfig> _configQueue = new BlockingCollection<BucketConfig>(new ConcurrentQueue<BucketConfig>());
        private readonly ConcurrentDictionary<string, BucketConfig> _configs = new ConcurrentDictionary<string, BucketConfig>();
        private CancellationTokenSource TokenSource { get; set; }
        private readonly Couchbase.Configuration _configuration;

        internal delegate void BucketConfigHandler(object sender, BucketConfigEventArgs a);
        private event BucketConfigHandler ConfigChanged;

        public ConfigContext(Couchbase.Configuration configuration)
        {
            _configuration = configuration;
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
                    await Task.Delay(2500, TokenSource.Token).ConfigureAwait(false);

                    foreach (var clusterNode in _configuration.GlobalNodes.Where(x=>x.Connection != null))
                    {
                        var config = await clusterNode.GetClusterMap().ConfigureAwait(false);
                        if (config != null)//TODO GetClusterMap should throw exception instead of return null on error
                        {
                            Publish(config);
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

        public void Subscribe(IBucketInternal bucket)
        {
            ConfigChanged += bucket.ConfigUpdated;
        }

        public void Unsubscribe(IBucketInternal bucket)
        {
            ConfigChanged -= bucket.ConfigUpdated;
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
