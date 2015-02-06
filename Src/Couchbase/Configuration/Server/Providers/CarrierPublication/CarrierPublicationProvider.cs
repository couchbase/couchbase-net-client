using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
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
using Timer = System.Timers.Timer;

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
                AutoReset = false
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
                    foreach (var server in value.Servers.Where(x => !x.IsDead))
                    {
                        try
                        {
                            var result = server.Send(new Config(Converter, server.EndPoint));
                            if (result.Success && result.Status == ResponseStatus.Success)
                            {
                                var config = result.Value;
                                if (config != null)
                                {
                                    UpdateConfig(result.Value);
                                    break; //break on first success
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Warn(e);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                _heartBeat.Start();
            }
        }

        public override IConfigInfo GetConfig(string bucketName, string password)
        {
            Log.Debug(m=>m("Getting config for bucket {0}", bucketName));
            var bucketConfiguration = GetOrCreateConfiguration(bucketName);

            //if the client is using a password make sure the client configuration references it
            password = string.IsNullOrEmpty(password) ? bucketConfiguration.Password : password;
            if (string.IsNullOrEmpty(bucketConfiguration.Password))
            {
                bucketConfiguration.Password = password;
            }

            var exceptions = new List<Exception>();
            CouchbaseConfigContext configInfo = null;
            foreach (var endPoint in bucketConfiguration.GetEndPoints())
            {
                Log.Debug(m=>m("Bootstrapping with {0}", endPoint));
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
                            Transcoder);

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
                        ioStrategy.Dispose();
                        throw new ConfigException("{0} is this a Memcached bucket?", operationResult.Value);
                    }
                    Log.Warn(m => m("Could not retrieve configuration for {0}. Reason: {1}",
                        bucketName,
                        operationResult.Message));
                }
                catch (ConfigException)
                {
                    Log.Debug(m => m("Bootstrapping with {0} failed.", endPoint));
                    throw;
                }
                catch (AuthenticationException e)
                {
                    const string msg =
                        "A failure to authenticate may mean that the server has not joined the cluster" +
                        " yet or that the Bucket does not exist. Please check that {0} has joined that" +
                        " cluster and that the Bucket '{1}' exists.";

                    Log.Warn(m => m(msg, endPoint, bucketName));
                    Log.Warn(e);
                    exceptions.Add(e);
                }
                catch (Exception e)
                {
                    Log.Debug(m => m("Bootstrapping with {0} failed.", endPoint));
                    Log.Warn(e);
                    exceptions.Add(e);
                }
            }

            //Client cannot bootstrap with this provider
            if (configInfo == null)
            {
                throw new AggregateException(exceptions);
            }

            return configInfo;
        }

        public override bool RegisterObserver(IConfigObserver observer)
        {
            return ConfigObservers.TryAdd(observer.Name, observer);
        }

        public void UpdateConfig(IBucketConfig bucketConfig, bool force = false)
        {
            IConfigObserver configObserver;
            if (ConfigObservers != null && ConfigObservers.TryGetValue(bucketConfig.Name, out configObserver))
            {
                IConfigInfo configInfo;
                if (Configs.TryGetValue(bucketConfig.Name, out configInfo))
                {
                    var lockTaken = false;
                    try
                    {
                        Monitor.TryEnter(configInfo, ref lockTaken);
                        if (!lockTaken) return;

                        var oldBucketConfig = configInfo.BucketConfig;
                        if (bucketConfig.Rev > oldBucketConfig.Rev || !bucketConfig.Equals(oldBucketConfig) || force)
                        {
                            Log.Info(
                                m =>
                                    m("Config changed (forced:{0}) new Rev#{1} | old Rev#{2} CCCP: {3}", force,
                                        bucketConfig.Rev, oldBucketConfig.Rev,
                                        JsonConvert.SerializeObject(bucketConfig)));

                            //Set the password on the new server configuration
                            var clientBucketConfig = GetOrCreateConfiguration(bucketConfig.Name);
                            bucketConfig.Password = clientBucketConfig.Password;

                            configInfo.LoadConfig(bucketConfig, force);
                            ClientConfig.UpdateBootstrapList(bucketConfig);
                            configObserver.NotifyConfigChanged(configInfo);
                        }
                    }
                    finally
                    {
                        if (lockTaken)
                        {
                            Monitor.Exit(configInfo);
                        }
                    }
                }
                else
                {
                    throw new ConfigNotFoundException(bucketConfig.Name);
                }
            }
            else
            {
                Log.Warn(m => m("No ConfigObserver found for bucket [{0}]", bucketConfig.Name));
            }
        }

        public override void UnRegisterObserver(IConfigObserver observer)
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

        public override void Dispose()
        {
            Log.Debug(m => m("Disposing ConfigurationProvider: {0}", GetType().Name));
            Dispose(true);
        }

        public void Dispose(bool disposing)
        {
            try
            {
                ConfigLock.EnterWriteLock();
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
            finally
            {
                ConfigLock.ExitWriteLock();
            }
        }

#if DEBUG
        ~CarrierPublicationProvider()
        {
            Log.Debug(m => m("Finalizing ConfigurationProvider: {0}", GetType().Name));
            Dispose(false);
        }
#endif
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
