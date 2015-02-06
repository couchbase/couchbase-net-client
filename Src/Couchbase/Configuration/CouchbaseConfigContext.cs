using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Couchbase.Authentication.SASL;
using Couchbase.Configuration.Client;
using Couchbase.Configuration.Server.Serialization;
using Couchbase.Core;
using Couchbase.Core.Buckets;
using Couchbase.Core.Transcoders;
using Couchbase.IO;
using Couchbase.IO.Converters;
using Couchbase.Utils;

namespace Couchbase.Configuration
{
    /// <summary>
    /// Represents a configuration context for a Couchbase Bucket.
    /// </summary>
    internal sealed class CouchbaseConfigContext : ConfigContextBase
    {
        public CouchbaseConfigContext(IBucketConfig bucketConfig, ClientConfiguration clientConfig,
            Func<IConnectionPool, IOStrategy> ioStrategyFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IOStrategy, IByteConverter, ISaslMechanism> saslFactory,
            IByteConverter converter,
            ITypeTranscoder transcoder)
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, converter, transcoder)
        {
        }

        /// <summary>
        /// Loads the most updated configuration creating any resources as needed.
        /// </summary>
        /// <param name="bucketConfig">The latest <see cref="IBucketConfig"/>
        /// that will drive the recreation if the configuration context.</param>
        /// <param name="force">True to force the reconfiguration.</param>
        public override void LoadConfig(IBucketConfig bucketConfig, bool force = false)
        {
            try
            {
                Lock.EnterWriteLock();
                if (BucketConfig == null || !BucketConfig.AreNodesEqual(bucketConfig) || !Servers.Any() || force)
                {
                    var clientBucketConfig = ClientConfig.BucketConfigs[bucketConfig.Name];
                    var servers = new List<IServer>();
                    var nodes = bucketConfig.GetNodes();
                    foreach (var adapter in nodes)
                    {
                        var endpoint = IPEndPointExtensions.GetEndPoint(adapter, clientBucketConfig, BucketConfig);
                        try
                        {
                            Log.Info(m => m("o1-Creating the Servers {0} list using rev#{1}", Servers.Count(), bucketConfig.Rev));
                            var poolConfiguration = ClientConfig.BucketConfigs[bucketConfig.Name].PoolConfiguration;
                            var connectionPool = ConnectionPoolFactory(poolConfiguration, endpoint);
                            var ioStrategy = IOStrategyFactory(connectionPool);
                            var saslMechanism = SaslFactory(bucketConfig.Name, bucketConfig.Password, ioStrategy,
                                Converter);
                            ioStrategy.SaslMechanism = saslMechanism;

                            var server = new Core.Server(ioStrategy, adapter, ClientConfig, bucketConfig);
                            servers.Add(server);
                        }
                        catch (Exception e)
                        {
                            Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                        }
                    }
                    var old = Interlocked.Exchange(ref Servers, servers);
                    if (old != null)
                    {
                        old.ForEach(x => x.Dispose());
                        old.Clear();
                    }
                }
                if (BucketConfig == null || !BucketConfig.IsVBucketServerMapEqual(bucketConfig) || force)
                {
                    Log.Info(m => m("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev));
                    Interlocked.Exchange(ref _bucketConfig, bucketConfig);
                    Interlocked.Exchange(ref KeyMapper, new VBucketKeyMapper(Servers, _bucketConfig.VBucketServerMap)
                    {
                        Rev = _bucketConfig.Rev
                    });
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        public void LoadConfig(IOStrategy ioStrategy)
        {
            try
            {
                Lock.EnterWriteLock();
                Log.Info(m => m("o2-Creating the Servers list using rev#{0}", BucketConfig.Rev));

                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var servers = new List<IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = IPEndPointExtensions.GetEndPoint(adapter, clientBucketConfig, BucketConfig);
                    try
                    {
                        IServer server = null;
                        if (Equals(ioStrategy.EndPoint, endpoint) || nodes.Count() == 1)
                        {
                            server = new Core.Server(ioStrategy, adapter, ClientConfig, BucketConfig);
                        }
                        else
                        {
                            var poolConfig = ClientConfig.BucketConfigs[BucketConfig.Name].PoolConfiguration;
                            var connectionPool = ConnectionPoolFactory(poolConfig, endpoint);
                            var newIoStrategy = IOStrategyFactory(connectionPool);
                            var saslMechanism = SaslFactory(BucketConfig.Name, BucketConfig.Password, newIoStrategy, Converter);
                            newIoStrategy.SaslMechanism = saslMechanism;
                            server = new Core.Server(newIoStrategy, adapter, ClientConfig, BucketConfig);
                        }
                        servers.Add(server);
                    }
                    catch (Exception e)
                    {
                        Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }

                Log.Info(m => m("Creating the KeyMapper list using rev#{0}", BucketConfig.Rev));
                var old = Interlocked.Exchange(ref Servers, servers);
                if (old != null)
                {
                    old.ForEach(x => x.Dispose());
                    old.Clear();
                }
                Interlocked.Exchange(ref KeyMapper, new VBucketKeyMapper(Servers, BucketConfig.VBucketServerMap)
                {
                    Rev = BucketConfig.Rev
                });
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        public override void LoadConfig()
        {
            Lock.EnterWriteLock();
            try
            {
                Log.Info(m => m("o3-Creating the Servers list using rev#{0}", BucketConfig.Rev));
                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var servers = new List<IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = IPEndPointExtensions.GetEndPoint(adapter, clientBucketConfig, BucketConfig);
                    try
                    {
                        var connectionPool = ConnectionPoolFactory(clientBucketConfig.PoolConfiguration,endpoint);
                        var ioStrategy = IOStrategyFactory(connectionPool);
                        var saslMechanism = SaslFactory(BucketConfig.Name, BucketConfig.Password, ioStrategy, Converter);
                        ioStrategy.SaslMechanism = saslMechanism;
                        var server = new Core.Server(ioStrategy, adapter, ClientConfig, BucketConfig);
                        servers.Add(server);
                    }
                    catch (Exception e)
                    {
                        Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }
                var old = Interlocked.Exchange(ref Servers, servers);
                if (old != null)
                {
                    old.ForEach(x => x.Dispose());
                    old.Clear();
                }
                Interlocked.Exchange(ref KeyMapper, new VBucketKeyMapper(Servers, BucketConfig.VBucketServerMap));
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        internal List<IServer> GetServers()
        {
            return Servers;
        }

        internal Dictionary<int, IVBucket> GetVBuckets()
        {
            return ((VBucketKeyMapper) KeyMapper).GetVBuckets();
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