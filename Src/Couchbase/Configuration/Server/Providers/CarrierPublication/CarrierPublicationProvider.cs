using System.Collections.Generic;
using System.Security.Authentication;
using System.Timers;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.IO.Operations;
using System;
using System.Net;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : ConfigProviderBase
    {
        private Timer _heartBeat;

        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
            : base(clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, transcoder)
        {
            _heartBeat = new Timer
            {
                Interval = ClientConfig.HeartbeatConfigInterval,
                Enabled = ClientConfig.EnableConfigHeartBeat,
                AutoReset = true
            };
            _heartBeat.Elapsed += _heartBeat_Elapsed;
        }

        void _heartBeat_Elapsed(object sender, ElapsedEventArgs args)
        {
            try
            {
                foreach (var configInfo in Configs)
                {
                    var value = configInfo.Value;
                    var server = value.GetServer();
                    var result = server.Send(new Config(Converter, server.EndPoint));
                    if (result.Success)
                    {
                        UpdateConfig(result.Value);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override IConfigInfo GetConfig(string bucketName, string password)
        {
            lock (SyncObj)
            {
                Log.Debug(m=>m("Getting config for bucket {0}", bucketName));
                var bucketConfiguration = GetOrCreateConfiguration(bucketName);
                password = string.IsNullOrEmpty(password) ? bucketConfiguration.Password : password;

                var exceptions = new List<Exception>();
                CouchbaseConfigContext configInfo = null;
                foreach (var endPoint in bucketConfiguration.GetEndPoints())
                {
                    try
                    {
                        var connectionPool = ConnectionPoolFactory(bucketConfiguration.PoolConfiguration, endPoint);
                        var ioStrategy = IOStrategyFactory(connectionPool);
                        var saslMechanism = SaslFactory(bucketName, password, ioStrategy, Converter);
                        ioStrategy.SaslMechanism = saslMechanism;

                        var operationResult = ioStrategy.Execute(new Config(Converter, endPoint));
                        if (operationResult.Success)
                        {
                            var bucketConfig = operationResult.Value;
                            bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString();
                            bucketConfig.Password = password;
                            configInfo = new CouchbaseConfigContext(bucketConfig,
                                ClientConfig,
                                IOStrategyFactory,
                                ConnectionPoolFactory,
                                SaslFactory,
                                Converter,
                                transcoder);

                            Log.Info(m => m("{0}", JsonConvert.SerializeObject(bucketConfig)));

                            configInfo.LoadConfig(ioStrategy);
                            Configs[bucketName] = configInfo;
                            break;
                        }
                        var exception = operationResult.Exception;
                        if (exception != null)
                        {
                            exceptions.Add(exception);
                        }

                        //CCCP only supported for Couchbase Buckets
                        if (operationResult.Status == ResponseStatus.UnknownCommand)
                        {
                            throw new ConfigException("{0} is this a Memcached bucket?", operationResult.Value);
                        }
                        Log.Warn(m => m("Could not retrieve configuration for {0}. Reason: {1}",
                            bucketName,
                            operationResult.Message));
                    }
                    catch (ConfigException)
                    {
                        throw;
                    }
                    catch (AuthenticationException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Log.Warn(e);
                    }
                }

                //Client cannot bootstrap with this provider
                if (configInfo == null)
                {
                    throw new AggregateException(exceptions);
                }

                return configInfo;
            }
        }

        public override bool RegisterObserver(IConfigObserver observer)
        {
            return ConfigObservers.TryAdd(observer.Name, observer);
        }

        public void UpdateConfig(IBucketConfig bucketConfig)
        {
            IConfigObserver configObserver;
            if (ConfigObservers.TryGetValue(bucketConfig.Name, out configObserver))
            {
                IConfigInfo configInfo;
                if (Configs.TryGetValue(bucketConfig.Name, out configInfo))
                {
                    var oldBucketConfig = configInfo.BucketConfig;
                    if (bucketConfig.Rev > oldBucketConfig.Rev)
                    {
                        Log.Info(m => m("New config has changed new Rev#{0} | old Rev#{1} CCCP: {2}", bucketConfig.Rev, oldBucketConfig.Rev, JsonConvert.SerializeObject(bucketConfig)));
                        configInfo.LoadConfig(bucketConfig);
                        UpdateBootstrapList(bucketConfig);
                        configObserver.NotifyConfigChanged(configInfo);
                    }
                }
                else
                {
                    throw new ConfigNotFoundException(bucketConfig.Name);
                }
            }
            else
            {
                Log.Warn(m=>m("No ConfigObserver found for bucket [{0}]", bucketConfig.Name));
            }
        }

        public override void UnRegisterObserver(IConfigObserver observer)
        {
            lock (SyncObj)
            {
                IConfigObserver observerToRemove;
                if (ConfigObservers.TryRemove(observer.Name, out observerToRemove))
                {
                    var temp = observerToRemove;
                    Log.Info(m => m("Unregistering observer {0}", temp.Name));

                    IConfigInfo configInfo;
                    if (Configs.TryRemove(observer.Name, out configInfo))
                    {
                        configInfo.Dispose();
                        Log.Info(m => m("Removing config for observer {0}", observer.Name));
                    }
                    else
                    {
                        Log.Warn(m => m("Could not remove config for {0}", observer.Name));
                    }
                }
                else
                {
                    Log.Warn(m => m("Could not unregister observer {0}", observer.Name));
                }
            }
        }

        public override void Dispose()
        {
            Log.Debug(m => m("Disposing ConfigurationProvider: {0}", GetType().Name));
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            lock (SyncObj)
            {
                if (!Disposed && disposing)
                {
                    GC.SuppressFinalize(this);
                }
                if (_heartBeat != null)
                {
                    _heartBeat.Dispose();
                }
                foreach (var configObserver in ConfigObservers)
                {
                    UnRegisterObserver(configObserver.Value);
                }
                ConfigObservers.Clear();
                Disposed = true;
            }
        }

        ~CarrierPublicationProvider()
        {
            Log.Debug(m => m("Finalizing ConfigurationProvider: {0}", GetType().Name));
            Dispose(false);
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion