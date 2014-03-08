using System;
using System.Collections.Concurrent;
using System.Threading;
using Common.Logging;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;

namespace Couchbase.Configuration.Server.Providers.Streaming
{
    internal class HttpStreamingProvider : IConfigProvider, IDisposable
    {
        private readonly ILog Log = LogManager.GetCurrentClassLogger();
        private IServerConfig _serverConfig;
        private readonly ClientConfiguration _clientConfig;

        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>(); 
        private readonly ConcurrentDictionary<string, Thread> _threads = new ConcurrentDictionary<string, Thread>(); 
        private readonly ConcurrentDictionary<string, IConfigInfo> _configs = new ConcurrentDictionary<string, IConfigInfo>();
        private readonly ConcurrentDictionary<string, IConfigListener> _listeners = new ConcurrentDictionary<string, IConfigListener>();
        private static readonly CountdownEvent CountdownEvent = new CountdownEvent(1);

        public HttpStreamingProvider(ClientConfiguration clientConfig)
        {
            _clientConfig = clientConfig;
        }

        public IConfigInfo GetCached(string bucketName)
        {
            throw new NotImplementedException();
        }

        public IConfigInfo GetConfig(string bucketName)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {
            if (_serverConfig == null)
            {
                _serverConfig = new HttpServerConfig(_clientConfig);
                _serverConfig.Initialize();

                Log.Debug(m=>m("Starting provider on main thread: {0}", Thread.CurrentThread.ManagedThreadId));
            }
        }

        public bool RegisterListener(IConfigListener listener)
        {
            var bucketConfig = _serverConfig.Buckets.Find(x => x.Name == listener.Name);
            if (bucketConfig == null)
            {
                throw new BucketNotFoundException(listener.Name);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            _cancellationTokens[listener.Name] = cancellationTokenSource;

            var configThreadState = new ConfigThreadState(bucketConfig, ConfigChangedHandler, ErrorOccurredHandler, cancellationTokenSource.Token);
            var thread = new Thread(configThreadState.ListenForConfigChanges);
            
            if (_threads.TryAdd(listener.Name, thread) && _listeners.TryAdd(listener.Name, listener))
            {
                _threads[listener.Name].Start();
               
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
            //4-notify the listener that a new configuration is available

            var listener = _listeners[bucketConfig.Name];

            IConfigInfo configInfo;
            if (_configs.ContainsKey(bucketConfig.Name))
            {
                configInfo = _configs[bucketConfig.Name];
                if (configInfo.BucketConfig.Equals(bucketConfig)) return;
                configInfo = new DefaultConfig(_clientConfig, _serverConfig)
                {
                    BucketConfig = bucketConfig
                };
            }
            else
            {
                configInfo = new DefaultConfig(_clientConfig, _serverConfig)
                {
                    BucketConfig = bucketConfig
                };
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
            if (CountdownEvent.CurrentCount > 0)
            {
                CountdownEvent.Signal();
            }
        }

        static void ErrorOccurredHandler(IBucketConfig bucketConfig)
        {
            //TODO provide implementation to begin the bootstrapping procss from the beginning
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void UnRegisterListener(IConfigListener listener)
        {
            Thread thread;
            if (_threads.TryRemove(listener.Name, out thread))
            {
                CancellationTokenSource cancellationTokenSource;
                if (_cancellationTokens.TryRemove(listener.Name, out cancellationTokenSource))
                {
                    Log.Info(m=>m("Cancelling {0}", listener.Name));
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }

                IConfigListener temp;
                if (_listeners.TryRemove(listener.Name, out temp))
                {
                    Log.Info(m=>m("Removing listener for {0}", listener.Name));
                }

                IConfigInfo configInfo = null;
                if (_configs.TryRemove(listener.Name, out configInfo))
                {
                    Log.Info(m=>m("Removing config for {0}", listener.Name));
                }
            }
        }


        public bool ListenerExists(IConfigListener listener)
        {
            throw new NotImplementedException();
        }
    }
}

