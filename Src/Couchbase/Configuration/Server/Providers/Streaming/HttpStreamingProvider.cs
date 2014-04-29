using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.IO;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    internal class HttpStreamingProvider : IConfigProvider, IDisposable
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IServerConfig _serverConfig;
        private readonly ClientConfiguration _clientConfig;

        private readonly Func<IConnectionPool, IOStrategy> _ioStrategyFactory;
        private readonly Func<PoolConfiguration, IPEndPoint, IConnectionPool> _connectionPoolFactory;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>(); 
        private readonly ConcurrentDictionary<string, Thread> _threads = new ConcurrentDictionary<string, Thread>(); 
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigObserver> _listeners = new ConcurrentDictionary<string, IConfigObserver>();
        private static readonly CountdownEvent CountdownEvent = new CountdownEvent(1);

        public HttpStreamingProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public HttpStreamingProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory)
        {
            _clientConfig = clientConfig;
            _ioStrategyFactory = ioStrategyFactory;
            _connectionPoolFactory = connectionPoolFactory;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            IConfigInfo configInfo;
            if (!_configs.TryGetValue(bucketName, out configInfo))
            {
                throw new ConfigNotFoundException(bucketName);
            }
            return configInfo;
        }

        public IConfigInfo GetConfig(string bucketName, string password)
        {
            StartProvider(bucketName, password);
            var bucketConfig = GetBucketConfig(bucketName, password);

            IConfigInfo configInfo = null;
            var nodes = bucketConfig.Nodes.ToList();
            while (nodes.Any())
            {
                try
                {
                    nodes.Shuffle();
                    var node = nodes.First();
                    nodes.Remove(node);

                    IBucketConfig newConfig;
                    var uri = bucketConfig.GetTerseUri(node);
                    using (var webClient = new AuthenticatingWebClient(bucketName, password))
                    {
                        var body = webClient.DownloadString(uri);
                        newConfig = JsonConvert.DeserializeObject<BucketConfig>(body);
                    }

                    configInfo = CreateConfigInfo(newConfig);
                    _configs[bucketName] = configInfo;
                    break;

                }
                catch (WebException e)
                {
                    Log.Error(e);
                }
                catch (IOException e)
                {
                    Log.Error(e);
                }
            }

            if (configInfo == null)
            {
                throw new BucketNotFoundException();
            }
            return configInfo;
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            return GetConfig(bucketName, string.Empty);
        }

        public bool RegisterObserver(IConfigObserver observer)
        {
            var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == observer.Name);
            if (bucketConfig == null)
            {
                throw new BucketNotFoundException(observer.Name);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokens[observer.Name] = cancellationTokenSource;

            var configThreadState = new ConfigThreadState(bucketConfig, ConfigChangedHandler, ErrorOccurredHandler, cancellationTokenSource.Token);
            var thread = new Thread(configThreadState.ListenForConfigChanges);
            
            if (_threads.TryAdd(observer.Name, thread) && _listeners.TryAdd(observer.Name, observer))
            {
                _threads[observer.Name].Start();
                
                if (CountdownEvent.CurrentCount == 0)
                {
                    CountdownEvent.Reset(1);
                }

                CountdownEvent.Wait();
            }
            return true;//todo fix
        }

        private void ConfigChangedHandler(IBucketConfig bucketConfig)
        {
            //1-Compare previous with current
            //2-if no change, then continue
            //3-else update configuration references
            //4-notify the observer that a new configuration is available

            var listener = _listeners[bucketConfig.Name];

            IConfigInfo configInfo;
            if (_configs.ContainsKey(bucketConfig.Name))
            {
                configInfo = _configs[bucketConfig.Name];
                if (configInfo.BucketConfig.Equals(bucketConfig))
                {
                    SignalCountdownEvent();
                    return;
                }
                configInfo = CreateConfigInfo(bucketConfig);
            }
            else
            {
                configInfo = CreateConfigInfo(bucketConfig);
                _configs.TryAdd(bucketConfig.Name, configInfo);
            }
            try
            {
                listener.NotifyConfigChanged(configInfo);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            SignalCountdownEvent();
        }

        void SignalCountdownEvent()
        {
            if (CountdownEvent.CurrentCount > 0)
            {
                CountdownEvent.Signal();
            }
        }

        IConfigInfo CreateConfigInfo(IBucketConfig bucketConfig)
        {
            IConfigInfo configInfo = null;
            switch (bucketConfig.NodeLocator.ToEnum<NodeLocatorEnum>())
            {
                case NodeLocatorEnum.VBucket:
                    configInfo = new CouchbaseConfigContext(bucketConfig,
                        _clientConfig,
                        _ioStrategyFactory,
                        _connectionPoolFactory);
                    break;
                case NodeLocatorEnum.Ketama:
                    configInfo = new MemcachedConfigContext(bucketConfig,
                        _clientConfig,
                        _ioStrategyFactory,
                        _connectionPoolFactory);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return configInfo;
        }

        void StartProvider(string username, string password)
        {
            _serverConfig = new HttpServerConfig(_clientConfig, username, password);
            _serverConfig.Initialize();
            Log.Debug(m => m("Starting provider on main thread: {0}", Thread.CurrentThread.ManagedThreadId));
        }

        IBucketConfig GetBucketConfig(string bucketName, string password)
        {
            var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == bucketName);
            if (bucketConfig == null)
            {
                throw new BucketNotFoundException(bucketName);
            }
            bucketConfig.Password = password;
            return bucketConfig;
        }

        void ErrorOccurredHandler(IBucketConfig bucketConfig)
        {
            //TODO provide implementation to begin the bootstrapping procss from the beginning
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void UnRegisterObserver(IConfigObserver observer)
        {
            Thread thread;
            if (_threads.TryRemove(observer.Name, out thread))
            {
                CancellationTokenSource cancellationTokenSource;
                if (_cancellationTokens.TryRemove(observer.Name, out cancellationTokenSource))
                {
                    Log.Info(m=>m("Cancelling {0}", observer.Name));
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }

                IConfigObserver temp;
                if (_listeners.TryRemove(observer.Name, out temp))
                {
                    Log.Info(m=>m("Removing observer for {0}", observer.Name));
                }

                IConfigInfo configInfo = null;
                if (_configs.TryRemove(observer.Name, out configInfo))
                {
                    Log.Info(m=>m("Removing config for {0}", observer.Name));
                }
            }
        }

        public bool ObserverExists(IConfigObserver observer)
        {
            throw new NotImplementedException();
        }
    }
}

