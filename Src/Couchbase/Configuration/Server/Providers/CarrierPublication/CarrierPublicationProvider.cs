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
using Couchbase.Core.Buckets;
using Couchbase.Tracing;
using Couchbase.Utils;
using Newtonsoft.Json;

namespace Couchbase.Configuration.Server.Providers.CarrierPublication
{
    internal class CarrierPublicationProvider : ConfigProviderBase
    {
        public CarrierPublicationProvider(ClientConfiguration clientConfig,
            Func<IConnectionPool, IIOService> ioServiceFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
            : base(clientConfig, ioServiceFactory, connectionPoolFactory, saslFactory, converter, transcoder)
        {
        }

        internal ICollection<IConfigInfo> ConfigContexts { get { return Configs.Values; } }

        public override IConfigInfo GetConfig(string bucketName, string username, string password)
        {
            Log.Debug("Getting config for bucket {0}", bucketName);
            var bucketConfiguration = GetOrCreateConfiguration(bucketName);

            //if the client is using a password make sure the client configuration references it
            password = string.IsNullOrEmpty(password) ? bucketConfiguration.Password : password;
            if (string.IsNullOrEmpty(bucketConfiguration.Password))
            {
                bucketConfiguration.Password = password;
            }

            var exceptions = new List<Exception>();
            CouchbaseConfigContext configInfo = null;
            foreach (var server in bucketConfiguration.Servers.Shuffle())
            {
                var port = bucketConfiguration.UseSsl ? BucketConfiguration.SslPort : bucketConfiguration.Port;
                var endPoint = server.GetIPEndPoint(port);
                Log.Debug("Bootstrapping with {0}", endPoint);

                IIOService ioService = null;
                try
                {
                    var poolConfig = bucketConfiguration.ClonePoolConfiguration(server);

                    var connectionPool = ConnectionPoolFactory(poolConfig, endPoint);
                    var saslMechanism = SaslFactory(username, password, connectionPool, Transcoder);
                    connectionPool.SaslMechanism = saslMechanism;

                    // setup IO service, this does SASL negotiation & hello
                    ioService = IOServiceFactory(connectionPool);

                    // finish initialising connection pool ready to be used
                    connectionPool.Initialize();

                    var operation = new Config(Transcoder, ClientConfig.DefaultOperationLifespan, endPoint);

                    IOperationResult<BucketConfig> operationResult;
                    using (poolConfig.ClientConfiguration.Tracer.StartParentSpan(operation, addIgnoreTag: true))
                    {
                        operationResult = ioService.Execute(operation);
                    }

                    if (operationResult.Success)
                    {
                        var bucketConfig = operationResult.Value;
                        if (string.IsNullOrWhiteSpace(bucketConfig.BucketType) && bucketConfig.BucketCapabilities != null)
                        {
                            bucketConfig.BucketType = (bucketConfig.BucketCapabilities.Contains("couchapi", StringComparer.OrdinalIgnoreCase)
                                ? BucketTypeEnum.Couchbase
                                : BucketTypeEnum.Ephemeral).ToString().ToLowerInvariant();
                        }
                        bucketConfig.SurrogateHost = connectionPool.EndPoint.Address.ToString();
                        bucketConfig.Password = password;
                        if (ClientConfig.UseSsl)
                        {
                            foreach (var ipEndPoint in bucketConfig.VBucketServerMap.IPEndPoints)
                            {
                                ipEndPoint.Port = ClientConfig.SslPort;
                            }
                        }

                        configInfo = new CouchbaseConfigContext(bucketConfig,
                            ClientConfig,
                            IOServiceFactory,
                            ConnectionPoolFactory,
                            SaslFactory,
                            Transcoder,
                            username,
                            password);

                        Log.Info("Bootstrap config: {0}", JsonConvert.SerializeObject(bucketConfig));

                        configInfo.LoadConfig(ioService);
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
                        const string message = "Config operation returned UnknownCommand.";
                        Log.Info(message);
                        exceptions.Add(new ConfigException(message));
                        break;
                    }

                    Log.Warn("Could not retrieve configuration for {0}. Reason: {1}",
                        bucketName,
                        operationResult.Message);
                }
                catch (AuthenticationException e)
                {
                    const string msg =
                        "A failure to authenticate may mean that the server has not joined the cluster" +
                        " yet or that the Bucket does not exist. Please check that {0} has joined that" +
                        " cluster and that the Bucket '{1}' exists. If using LDAP, please set" +
                        " forceSaslPlain to true.";

                    Log.Warn(msg, endPoint, bucketName);
                    Log.Warn(e);
                    exceptions.Add(e);
                }
                catch (Exception e)
                {
                    Log.Debug("Bootstrapping with {0} failed.", endPoint);
                    Log.Warn(e);
                    exceptions.Add(e);
                }
                finally
                {
                    if (ioService != null && configInfo == null)
                    {
                        ioService.Dispose();
                    }
                }
            }

            //Client cannot bootstrap with this provider
            if (configInfo == null)
            {
                throw new AggregateException("Could not bootstrap with CCCP.", exceptions);
            }

            return configInfo;
        }

        public override bool RegisterObserver(IConfigObserver observer)
        {
            return ConfigObservers.TryAdd(observer.Name, observer);
        }

        /// <summary>
        /// Updates the new configuration if the new configuration revision is greater than the current configuration.
        /// </summary>
        /// <param name="bucketConfig">The bucket configuration.</param>
        /// <param name="force">if set to <c>true</c> [force].</param>
        public override void UpdateConfig(IBucketConfig bucketConfig, bool force = false)
        {
            IConfigObserver configObserver;
            if (ConfigObservers != null && ConfigObservers.TryGetValue(bucketConfig.Name, out configObserver))
            {
                IConfigInfo configInfo;
                if (Configs.TryGetValue(bucketConfig.Name, out configInfo))
                {
                    try
                    {
                        Log.Debug("1. Checking config with rev#{0} on thread {1}", bucketConfig.Rev, Thread.CurrentThread.ManagedThreadId);
                        var oldBucketConfig = configInfo.BucketConfig;
                        if (bucketConfig.Rev > oldBucketConfig.Rev)
                        {
                            lock (SyncObj)
                            {
                                Log.Debug("2. Checking config with rev#{0} on thread {1}", bucketConfig.Rev, Thread.CurrentThread.ManagedThreadId);
                                if (bucketConfig.Rev > oldBucketConfig.Rev || !bucketConfig.Equals(oldBucketConfig) ||
                                    force)
                                {
                                    Log.Info(
                                        "Config changed (forced:{0}) new Rev#{1} | old Rev#{2} CCCP: {3}", force,
                                        bucketConfig.Rev, oldBucketConfig.Rev,
                                        JsonConvert.SerializeObject(bucketConfig));

                                    //Set the password on the new server configuration
                                    var clientBucketConfig = GetOrCreateConfiguration(bucketConfig.Name);
                                    bucketConfig.Password = clientBucketConfig.Password;

                                    configInfo.LoadConfig(bucketConfig, force);
                                    ClientConfig.UpdateBootstrapList(bucketConfig);
                                    configObserver.NotifyConfigChanged(configInfo);
                                    Log.Debug("3. Completed checking config with rev#{0} on thread {1}", bucketConfig.Rev, Thread.CurrentThread.ManagedThreadId);
                                }
                            }
                        }
                        else
                        {
                            Log.Info("Ignoring config with rev#{0}", bucketConfig.Rev);
                        }
                    }
                    catch(Exception e)
                    {
                        Log.Debug("Ack! rev#{0} on thread {1}", bucketConfig.Rev, Thread.CurrentThread.ManagedThreadId);
                        Log.Info(e);
                    }
                }
                else
                {
                    throw new ConfigNotFoundException(bucketConfig.Name);
                }
            }
            else
            {
                Log.Warn("No ConfigObserver found for bucket [{0}]", bucketConfig.Name);
            }
        }

        public override void UnRegisterObserver(IConfigObserver observer)
        {
            IConfigObserver observerToRemove;
            if (ConfigObservers.TryRemove(observer.Name, out observerToRemove))
            {
                var temp = observerToRemove;
                Log.Info("Unregistering observer {0}", temp.Name);

                IConfigInfo configInfo;
                if (Configs.TryRemove(observer.Name, out configInfo))
                {
                    configInfo.Dispose();
                    Log.Info("Removing config for observer {0}", observer.Name);
                }
                else
                {
                    Log.Warn("Could not remove config for {0}", observer.Name);
                }
            }
            else
            {
                Log.Warn("Could not unregister observer {0}", observer.Name);
            }
        }

        public override void Dispose()
        {
            Log.Debug("Disposing ConfigurationProvider: {0}", GetType().Name);
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
            Log.Debug("Finalizing ConfigurationProvider: {0}", GetType().Name);
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
