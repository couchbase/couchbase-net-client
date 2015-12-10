using System;
using System.Collections.Concurrent;
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
using Couchbase.N1QL;
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
            Func<string, string, IOStrategy, ITypeTranscoder, ISaslMechanism> saslFactory,
            ITypeTranscoder transcoder)
            : base(bucketConfig, clientConfig, ioStrategyFactory, connectionPoolFactory, saslFactory, transcoder)
        {
            //for caching query plans
            QueryCache = new ConcurrentDictionary<string, QueryPlan>();
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
                var nodes = bucketConfig.GetNodes();
                if (BucketConfig == null || !nodes.AreEqual(_bucketConfig.GetNodes()) || !Servers.Any() || force)
                {
                    Log.InfoFormat("o1-Creating the Servers {0} list using rev#{1}", nodes.Count, bucketConfig.Rev);

                    var queryUris = new ConcurrentBag<FailureCountingUri>();
                    var clientBucketConfig = ClientConfig.BucketConfigs[bucketConfig.Name];
                    var servers = new Dictionary<IPAddress, IServer>();
                    foreach (var adapter in nodes)
                    {
                        var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                        try
                        {
                            Log.InfoFormat("Creating node {0} for rev#{1}", endpoint, bucketConfig.Rev);
                            IServer server;
                            if (adapter.IsQueryNode)
                            {
                                var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                                uri.ConfigureServicePoint(ClientConfig);
                                queryUris.Add(uri);
                            }
                            if (adapter.IsDataNode) //a data node so create a connection pool
                            {
                                var poolConfiguration = ClientConfig.BucketConfigs[bucketConfig.Name].PoolConfiguration;
                                var connectionPool = ConnectionPoolFactory(poolConfiguration, endpoint);
                                var ioStrategy = IOStrategyFactory(connectionPool);

                                server = new Core.Server(ioStrategy, adapter, ClientConfig, bucketConfig, Transcoder, QueryCache)
                                {
                                    SaslFactory = SaslFactory
                                };
                                server.CreateSaslMechanismIfNotExists();
                                SupportsEnhancedDurability = ioStrategy.SupportsEnhancedDurability;
                            }
                            else
                            {
                                server = new Core.Server(null, adapter, ClientConfig, bucketConfig, Transcoder, QueryCache);
                            }
                            servers.Add(endpoint.Address, server);
                        }
                        catch (Exception e)
                        {
                            Log.ErrorFormat("Could not add server {0} for rev#{1}. Exception: {2}", endpoint, bucketConfig.Rev, e);
                        }
                    }

                    UpdateServices(servers);

                    //for caching uri's
                    Interlocked.Exchange(ref QueryUris, queryUris);

                    var old = Interlocked.Exchange(ref Servers, servers);
                    Log.Info(m => m("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev));
                    var vBucketKeyMapper = new VBucketKeyMapper(Servers, bucketConfig.VBucketServerMap, bucketConfig.Rev);
                    Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                    Interlocked.Exchange(ref _bucketConfig, bucketConfig);

                    if (old != null)
                    {
                        foreach (var server in old.Values)
                        {
                            Log.InfoFormat("Disposing node {0} from rev#{1}", server.EndPoint, server.Revision);
                            server.Dispose();
                        }
                        old.Clear();
                    }
                }
                else
                {
                    if (BucketConfig == null || !BucketConfig.IsVBucketServerMapEqual(bucketConfig) || force)
                    {
                        Log.Info(m => m("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev));
                        var vBucketKeyMapper = new VBucketKeyMapper(Servers, bucketConfig.VBucketServerMap,
                            bucketConfig.Rev);
                        Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                        Interlocked.Exchange(ref _bucketConfig, bucketConfig);
                    }
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

        /// <exception cref="CouchbaseBootstrapException">Condition.</exception>
        public void LoadConfig(IOStrategy ioStrategy)
        {
            var supportsEnhancedDurability = false;
            try
            {
                Lock.EnterWriteLock();
                Log.Info(m => m("o2-Creating the Servers list using rev#{0}", BucketConfig.Rev));

                var queryUris = new ConcurrentBag<FailureCountingUri>();
                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var servers = new Dictionary<IPAddress, IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                    try
                    {
                        IServer server = null;
                        if (Equals(ioStrategy.EndPoint, endpoint) || nodes.Count() == 1)
                        {
                            server = new Core.Server(ioStrategy, adapter, ClientConfig, BucketConfig, Transcoder, QueryCache);
                            supportsEnhancedDurability = ioStrategy.SupportsEnhancedDurability;
                            SupportsEnhancedDurability = supportsEnhancedDurability;
                            if (server.IsQueryNode)
                            {
                                var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                                uri.ConfigureServicePoint(ClientConfig);
                                queryUris.Add(uri);
                            }
                        }
                        else
                        {
                            if (adapter.IsQueryNode)
                            {
                                var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                                uri.ConfigureServicePoint(ClientConfig);
                                queryUris.Add(uri);
                            }
                            if (adapter.IsDataNode) //a data node so create a connection pool
                            {
                                var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name].PoolConfiguration;
                                var connectionPool = ConnectionPoolFactory(poolConfiguration, endpoint);
                                var newIoStrategy = IOStrategyFactory(connectionPool);

                                server = new Core.Server(newIoStrategy, adapter, ClientConfig, BucketConfig, Transcoder, QueryCache)
                                {
                                    SaslFactory = SaslFactory
                                };
                                server.CreateSaslMechanismIfNotExists();

                                //Note: "ioStrategy has" already made a HELO command to check if
                                //the cluster supports enhanced durability so we are reusing the flag
                                //instead of having "newIoStrategy" do it again, later.
                                SupportsEnhancedDurability = ioStrategy.SupportsEnhancedDurability;
                            }
                            else
                            {
                                server = new Core.Server(null, adapter, ClientConfig, BucketConfig, Transcoder, QueryCache);
                            }
                        }
                        servers.Add(endpoint.Address, server);
                    }
                    catch (Exception e)
                    {
                        Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }

                UpdateServices(servers);

                //for caching uri's
                Interlocked.Exchange(ref QueryUris, queryUris);

                Log.Info(m => m("Creating the KeyMapper list using rev#{0}", BucketConfig.Rev));
                var old = Interlocked.Exchange(ref Servers, servers);
                var vBucketKeyMapper = new VBucketKeyMapper(Servers, BucketConfig.VBucketServerMap, BucketConfig.Rev);
                Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                if (old != null)
                {
                    foreach (var server in old.Values)
                    {
                        server.Dispose();
                    }
                    old.Clear();
                }
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <exception cref="CouchbaseBootstrapException">Condition.</exception>
        public override void LoadConfig()
        {
            Lock.EnterWriteLock();
            try
            {
                Log.Info(m => m("o3-Creating the Servers list using rev#{0}", BucketConfig.Rev));
                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var queryUris = new ConcurrentBag<FailureCountingUri>();
                var servers = new Dictionary<IPAddress, IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                    try
                    {
                        IServer server;
                        if (adapter.IsQueryNode)
                        {
                            var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                            uri.ConfigureServicePoint(ClientConfig);
                            queryUris.Add(uri);
                        }
                        if (adapter.IsDataNode) //a data node so create a connection pool
                        {
                            var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name].PoolConfiguration;
                            var connectionPool = ConnectionPoolFactory(poolConfiguration, endpoint);
                            var newIoStrategy = IOStrategyFactory(connectionPool);

                            server = new Core.Server(newIoStrategy, adapter, ClientConfig, BucketConfig, Transcoder, QueryCache)
                            {
                                SaslFactory = SaslFactory
                            };
                            server.CreateSaslMechanismIfNotExists();
                            SupportsEnhancedDurability = newIoStrategy.SupportsEnhancedDurability;
                        }
                        else
                        {
                            server = new Core.Server(null, adapter, ClientConfig, BucketConfig, Transcoder, QueryCache);
                        }
                        servers.Add(endpoint.Address, server);
                    }
                    catch (Exception e)
                    {
                        Log.ErrorFormat("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }

                UpdateServices(servers);

                //for caching uri's
                Interlocked.Exchange(ref QueryUris, queryUris);

                var old = Interlocked.Exchange(ref Servers, servers);
                var vBucketKeyMapper = new VBucketKeyMapper(Servers, BucketConfig.VBucketServerMap, BucketConfig.Rev);
                Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                if (old != null)
                {
                    foreach (var server in old.Values)
                    {
                        server.Dispose();
                    }
                    old.Clear();
                }
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Checks the server's list and identfies what services the node supports. Separate
        /// lists are created for each service type.
        /// </summary>
        /// <param name="servers">The servers.</param>
        /// <exception cref="Couchbase.Configuration.CouchbaseBootstrapException"></exception>
        void UpdateServices(Dictionary<IPAddress, IServer> servers)
        {
            //If servers is empty that means we could not initialize _any_ nodes
            //We fail-fast here so that the problem can be indentified and handled.
            if (!servers.Any())
            {
                throw new CouchbaseBootstrapException(ExceptionUtil.BootStrapFailedMsg);
            }

            var newQueryNodes = servers
                .Where(x => x.Value.IsQueryNode)
                .Select(x => x.Value)
                .ToList();

            Interlocked.Exchange(ref QueryNodes, newQueryNodes);
            IsQueryCapable = QueryNodes.Count > 0;

            var newDataNodes = servers
                .Where(x => x.Value.IsDataNode)
                .Select(x => x.Value)
                .ToList();

            Interlocked.Exchange(ref DataNodes, newDataNodes);
            IsDataCapable = DataNodes.Count > 0;

            var newViewNodes = servers
                .Where(x => x.Value.IsViewNode)
                .Select(x => x.Value)
                .ToList();

            Interlocked.Exchange(ref ViewNodes, newViewNodes);
            IsViewCapable = ViewNodes.Count > 0;

            var newIndexNodes = servers
                .Where(x => x.Value.IsIndexNode)
                .Select(x => x.Value)
                .ToList();

            Interlocked.Exchange(ref IndexNodes, newIndexNodes);
            IsIndexCapable = ViewNodes.Count > 0;

        }

        internal List<IServer> GetServers()
        {
            return Servers.Values.ToList();
        }

        internal Dictionary<int, IVBucket> GetVBuckets()
        {
            return ((VBucketKeyMapper) KeyMapper).GetVBuckets();
        }


        /// <summary>
        /// Gets the query cache for the current instance. Each <see cref="IBucket" /> implementation instance has it's own for caching query plans.
        /// </summary>
        /// <value>
        /// The query cache.
        /// </value>
        public ConcurrentDictionary<string, QueryPlan> QueryCache { get; private set; }
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