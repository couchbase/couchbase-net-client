using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication;
using System.Threading;
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
using Couchbase.Utils;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal sealed class CarrierPublicationProvider : ConfigProviderBase
    {
        private Timer _heartBeat;

        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
            : base(clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, transcoder)
        {            
            _heartBeat = new Timer(
                _heartBeat_Elapsed, 
                null, 
                ClientConfig.EnableConfigHeartBeat ? 0 : System.Threading.Timeout.Infinite,
                System.Threading.Timeout.Infinite);
        }

        void _heartBeat_Elapsed(object sender)
        {
            try
            {
                foreach (var configInfo in Configs)
                {
                    var value = configInfo.Value;
                    foreach (var server in value.Servers.Where(x => !x.IsDown && x.IsDataNode))
                    {
                        try
                        {
                            Log.Debug($"Config heartbeat on {server.EndPoint}.");
                            var result = server.Send(
                                new Config(Transcoder, ClientConfig.DefaultOperationLifespan, server.EndPoint));

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
                _heartBeat.Change(0, (int)ClientConfig.HeartbeatConfigInterval);
            }
        }

        public override IConfigInfo GetConfig(string bucketName, string password)
        {
            Log.Debug($"Getting config for bucket {bucketName}");
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
                Log.Debug($"Bootstrapping with {endPoint}");

                IOStrategy ioStrategy = null;
                try
                {
                    var connectionPool = ConnectionPoolFactory(bucketConfiguration.PoolConfiguration, endPoint);
                    ioStrategy = IOStrategyFactory(connectionPool);
                    var saslMechanism = SaslFactory(bucketName, password, ioStrategy, Transcoder);
                    ioStrategy.SaslMechanism = saslMechanism;

                    var operationResult = ioStrategy.Execute(
                        new Config(Transcoder, ClientConfig.DefaultOperationLifespan, endPoint));

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
                            Transcoder);

                        Log.Info($"Bootstrap config: {JsonConvert.SerializeObject(bucketConfig)}");

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
                    Log.Warn($"Could not retrieve configuration for {bucketName}. Reason: {operationResult.Message}");
                }
                catch (ConfigException)
                {
                    ioStrategy.Dispose();
                    Log.Debug($"Bootstrapping with {endPoint} failed.");
                    throw;
                }
                catch (AuthenticationException e)
                {
                    Log.Warn($"A failure to authenticate may mean that the server has not joined the cluster" +
                             " yet or that the Bucket does not exist. Please check that {endPoint} has joined that" +
                             " cluster and that the Bucket '{bucketName}' exists.");
                    Log.Warn(e);
                    exceptions.Add(e);
                }
                catch (Exception e)
                {
                    Log.Debug($"Bootstrapping with {endPoint} failed.");
                    Log.Warn(e);
                    exceptions.Add(e);
                }
                finally
                {
                    if (ioStrategy != null && configInfo == null)
                    {
                        ioStrategy.Dispose();
                    }
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
                            Log.Info($"Config changed (forced:{force}) new Rev#{bucketConfig.Rev}" +
                                      " | old Rev#{oldBucketConfig.Rev} CCCP: {JsonConvert.SerializeObject(bucketConfig)}");

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
                Log.Warn($"No ConfigObserver found for bucket [{bucketConfig.Name}]");
            }
        }

        public override void UnRegisterObserver(IConfigObserver observer)
        {
            IConfigObserver observerToRemove;
            if (ConfigObservers.TryRemove(observer.Name, out observerToRemove))
            {
                var temp = observerToRemove;
                Log.Info($"Unregistering observer {temp.Name}");

                IConfigInfo configInfo;
                if (Configs.TryRemove(observer.Name, out configInfo))
                {
                    configInfo.Dispose();
                    Log.Info($"Removing config for observer {observer.Name}");
                }
                else
                {
                    Log.Warn($"Could not remove config for {observer.Name}");
                }
            }
            else
            {
                Log.Warn($"Could not unregister observer {observer.Name}");
            }
        }

        public override void Dispose()
        {
            Log.Debug($"Disposing ConfigurationProvider: {GetType().Name}");
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
            Log.Debug($"Finalizing ConfigurationProvider: {GetType().Name}");
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
