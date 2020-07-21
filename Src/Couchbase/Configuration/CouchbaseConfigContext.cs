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
            Func<IConnectionPool, IIOService> ioServiceFactory,
            Func<PoolConfiguration, IPEndPoint, IConnectionPool> connectionPoolFactory,
            Func<string, string, IConnectionPool, ITypeTranscoder, ISaslMechanism> saslFactory,
            ITypeTranscoder transcoder,
            string userName,
            string password)
            : base(bucketConfig, clientConfig, ioServiceFactory, connectionPoolFactory, saslFactory, transcoder, userName, password)
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
                if (BucketConfig == null || !nodes.AreEqual(BucketConfig.GetNodes()) || !Servers.Any() || force)
                {
                    Log.Info("o1-Creating the Servers {0} list using rev#{1}", nodes.Count, bucketConfig.Rev);

                    var searchUris = new ConcurrentBag<FailureCountingUri>();
                    var queryUris = new ConcurrentBag<FailureCountingUri>();
                    var analyticsUris = new ConcurrentBag<FailureCountingUri>();
                    var clientBucketConfig = ClientConfig.BucketConfigs[bucketConfig.Name];
                    var servers = new Dictionary<IPEndPoint, IServer>();
                    foreach (var adapter in nodes)
                    {
                        var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                        try
                        {
                            //The node does not have to be created or swapped out so reuse the existing mode
                            if (Servers.TryGetValue(endpoint, out IServer cachedServer))
                            {
                                //The services list may have changed even though the
                                //connections can be reused so use the latest settings
                                cachedServer.LoadNodeAdapter(adapter, bucketConfig.Rev);

                                Log.Info("Reusing node {0} for rev#{1}", endpoint, bucketConfig.Rev);
                                servers.Add(endpoint, cachedServer);
                            }
                            else
                            {
                                Log.Info("Creating node {0} for rev#{1}", endpoint, bucketConfig.Rev);

                                IServer server;
                                if (adapter.IsDataNode) //a data node so create a connection pool
                                {
                                    var uri = UrlUtil.GetBaseUri(adapter, clientBucketConfig);
                                    var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name]
                                        .ClonePoolConfiguration(uri);

                                    var ioService = CreateIOService(poolConfiguration, endpoint);

                                    server = new Core.Server(ioService, adapter, Transcoder, QueryCache, this);

                                    SupportsEnhancedDurability = ioService.SupportsEnhancedDurability;
                                    SupportsSubdocXAttributes = ioService.SupportsSubdocXAttributes;
                                    SupportsEnhancedAuthentication = ioService.SupportsEnhancedAuthentication;
                                    SupportsKvErrorMap = ioService.SupportsKvErrorMap;
                                }
                                else
                                {
                                    server = new Core.Server(null, adapter, Transcoder, QueryCache, this);
                                }

                                servers.Add(endpoint, server);
                            }

                            if (adapter.IsSearchNode)
                            {
                                var uri = UrlUtil.GetFailureCountinSearchBaseUri(adapter, clientBucketConfig);
                                searchUris.Add(uri);
                            }
                            if (adapter.IsQueryNode)
                            {
                                var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                                queryUris.Add(uri);
                            }
                            if (adapter.IsAnalyticsNode)
                            {
                                var uri = UrlUtil.GetFailureCountingAnalyticsUri(adapter, clientBucketConfig);
                                analyticsUris.Add(uri);
                            }
                        }
                        catch (Exception e)
                        {
                            Log.Error("Could not add server {0} for rev#{1}. Exception: {2}", endpoint, bucketConfig.Rev, e);
                        }
                    }

                    UpdateServices(servers);

                    //for caching uri's
                    Interlocked.Exchange(ref QueryUris, queryUris);
                    Interlocked.Exchange(ref SearchUris, searchUris);
                    Interlocked.Exchange(ref AnalyticsUris, analyticsUris);

                    SwapServers(servers);

                    Log.Info("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev);
                    var vBucketKeyMapper = new VBucketKeyMapper(Servers.Where(x=>x.Value.IsDataNode).ToDictionary(x=>x.Key, x=>x.Value),
                        bucketConfig.GetBucketServerMap(clientBucketConfig.UseSsl),
                        bucketConfig.Rev,
                        bucketConfig.Name);

                    Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                    Interlocked.Exchange(ref _bucketConfig, bucketConfig);
                }
                else
                {
                    if (BucketConfig == null || !BucketConfig.IsVBucketServerMapEqual(bucketConfig) || force)
                    {
                        Log.Info("Creating the KeyMapper list using rev#{0}", bucketConfig.Rev);

                        var clientBucketConfig = ClientConfig.BucketConfigs[bucketConfig.Name];
                        foreach (var adapter in nodes)
                        {
                            var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                            var server = Servers[endpoint];
                            if (adapter.IsDataNode && !server.IsDataNode)
                            {
                                var uri = UrlUtil.GetBaseUri(adapter, clientBucketConfig);
                                var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name]
                                    .ClonePoolConfiguration(uri);

                                var ioService = CreateIOService(poolConfiguration, endpoint);

                                var newServer = new Core.Server(ioService, adapter, Transcoder, QueryCache, this);
                                server.Dispose();
                                Servers[endpoint] = newServer;
                            }
                        }

                        var dataNodes = Servers.Where(x => x.Value.IsDataNode).ToDictionary(x => x.Key, x => x.Value);

                        var vBucketKeyMapper = new VBucketKeyMapper(dataNodes,
                            bucketConfig.GetBucketServerMap(ClientConfig.BucketConfigs[bucketConfig.Name].UseSsl),
                            bucketConfig.Rev,
                            bucketConfig.Name);

                        Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
                        Interlocked.Exchange(ref _bucketConfig, bucketConfig);
                    }
                    //only the revision changed so update to it
                    if (bucketConfig.Rev > BucketConfig.Rev)
                    {
                        Log.Info("Only the revision changed from using rev#{0} to rev#{1}", BucketConfig.Rev, bucketConfig.Rev);
                        BucketConfig.Rev = bucketConfig.Rev;
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
        public void LoadConfig(IIOService ioService)
        {
            try
            {
                Lock.EnterWriteLock();
                Log.Info("o2-Creating the Servers list using rev#{0}", BucketConfig.Rev);

                var searchUris = new ConcurrentBag<FailureCountingUri>();
                var queryUris = new ConcurrentBag<FailureCountingUri>();
                var analyticsUris = new ConcurrentBag<FailureCountingUri>();
                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var servers = new Dictionary<IPEndPoint, IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);
                    try
                    {
                        IServer server = null;

                        //The node does not have to be created or swapped out so reuse the existing mode
                        if (Servers.TryGetValue(endpoint, out IServer cachedServer))
                        {
                            //The services list may have changed even though the
                            //connections can be reused so use the latest settings
                            cachedServer.LoadNodeAdapter(adapter, BucketConfig.Rev);

                            Log.Info("Reusing node {0} for rev#{1}", endpoint, BucketConfig.Rev);
                            servers.Add(endpoint, cachedServer);
                        }
                        else
                        {
                            Log.Info("Creating node {0} for rev#{1}", endpoint, BucketConfig.Rev);
                            if (Equals(ioService.EndPoint, endpoint) || nodes.Count == 1)
                            {
                                server = new Core.Server(ioService, adapter, Transcoder, QueryCache, this);
                                SupportsEnhancedDurability = ioService.SupportsEnhancedDurability;
                                SupportsSubdocXAttributes = ioService.SupportsSubdocXAttributes;
                                SupportsEnhancedAuthentication = ioService.SupportsEnhancedAuthentication;
                                SupportsKvErrorMap = ioService.SupportsKvErrorMap;
                            }
                            else
                            {
                                if (adapter.IsDataNode) //a data node so create a connection pool
                                {
                                    var uri = UrlUtil.GetBaseUri(adapter, clientBucketConfig);
                                    var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name]
                                        .ClonePoolConfiguration(uri);

                                    var newIoService = CreateIOService(poolConfiguration, endpoint);

                                    server = new Core.Server(newIoService, adapter, Transcoder, QueryCache, this);

                                    //Note: "ioService has" already made a HELO command to check what features
                                    //the cluster supports (eg enhanced durability) so we are reusing the flag
                                    //instead of having "newIoService" do it again, later.
                                    SupportsEnhancedDurability = ioService.SupportsEnhancedDurability;
                                    SupportsSubdocXAttributes = ioService.SupportsSubdocXAttributes;
                                    SupportsEnhancedAuthentication = ioService.SupportsEnhancedAuthentication;
                                    SupportsKvErrorMap = ioService.SupportsKvErrorMap;
                                }
                                else
                                {
                                    server = new Core.Server(null, adapter, Transcoder, QueryCache, this);
                                }
                            }
                        }

                        if (adapter.IsQueryNode)
                        {
                            var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                            queryUris.Add(uri);
                        }
                        if (adapter.IsSearchNode)
                        {
                            var uri = UrlUtil.GetFailureCountinSearchBaseUri(adapter, clientBucketConfig);
                            searchUris.Add(uri);
                        }
                        if (adapter.IsAnalyticsNode)
                        {
                            var uri = UrlUtil.GetFailureCountingAnalyticsUri(adapter, clientBucketConfig);
                            analyticsUris.Add(uri);
                        }

                        servers.Add(endpoint, server);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }

                UpdateServices(servers);

                //for caching uri's
                Interlocked.Exchange(ref QueryUris, queryUris);
                Interlocked.Exchange(ref SearchUris, searchUris);
                Interlocked.Exchange(ref AnalyticsUris, analyticsUris);

                SwapServers(servers);

                Log.Info("Creating the KeyMapper list using rev#{0}", BucketConfig.Rev);
                var vBucketKeyMapper = new VBucketKeyMapper(Servers.Where(x=>x.Value.IsDataNode).ToDictionary(x=>x.Key, x=>x.Value),
                    BucketConfig.GetBucketServerMap(clientBucketConfig.UseSsl),
                    BucketConfig.Rev,
                    BucketConfig.Name);

                Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
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
                Log.Info("o3-Creating the Servers list using rev#{0}", BucketConfig.Rev);
                var clientBucketConfig = ClientConfig.BucketConfigs[BucketConfig.Name];
                var searchUris = new ConcurrentBag<FailureCountingUri>();
                var queryUris = new ConcurrentBag<FailureCountingUri>();
                var analyticsUris = new ConcurrentBag<FailureCountingUri>();
                var servers = new Dictionary<IPEndPoint, IServer>();
                var nodes = BucketConfig.GetNodes();
                foreach (var adapter in nodes)
                {
                    var endpoint = adapter.GetIPEndPoint(clientBucketConfig.UseSsl);

                    try
                    {
                        IServer server;
                        if (adapter.IsSearchNode)
                        {
                            var uri = UrlUtil.GetFailureCountinSearchBaseUri(adapter, clientBucketConfig);
                            searchUris.Add(uri);
                        }
                        if (adapter.IsQueryNode)
                        {
                            var uri = UrlUtil.GetFailureCountingBaseUri(adapter, clientBucketConfig);
                            queryUris.Add(uri);
                        }
                        if (adapter.IsAnalyticsNode)
                        {
                            var uri = UrlUtil.GetFailureCountingAnalyticsUri(adapter, clientBucketConfig);
                            analyticsUris.Add(uri);
                        }
                        if (adapter.IsDataNode) //a data node so create a connection pool
                        {
                            var uri = UrlUtil.GetBaseUri(adapter, clientBucketConfig);
                            var poolConfiguration = ClientConfig.BucketConfigs[BucketConfig.Name].ClonePoolConfiguration(uri);

                            var newIoService = CreateIOService(poolConfiguration, endpoint);

                            server = new Core.Server(newIoService, adapter, Transcoder, QueryCache, this);

                            SupportsEnhancedDurability = newIoService.SupportsEnhancedDurability;
                            SupportsSubdocXAttributes = newIoService.SupportsSubdocXAttributes;
                            SupportsEnhancedAuthentication = newIoService.SupportsEnhancedAuthentication;
                            SupportsKvErrorMap = newIoService.SupportsKvErrorMap;
                        }
                        else
                        {
                            server = new Core.Server(null, adapter, Transcoder, QueryCache, this);
                        }
                        servers.Add(endpoint, server);
                    }
                    catch (Exception e)
                    {
                        Log.Error("Could not add server {0}. Exception: {1}", endpoint, e);
                    }
                }

                UpdateServices(servers);

                //for caching uri's
                Interlocked.Exchange(ref QueryUris, queryUris);
                Interlocked.Exchange(ref SearchUris, searchUris);
                Interlocked.Exchange(ref AnalyticsUris, analyticsUris);

                SwapServers(servers);

                var vBucketKeyMapper = new VBucketKeyMapper(Servers.Where(x=>x.Value.IsDataNode).ToDictionary(x=>x.Key, x=>x.Value),
                    BucketConfig.GetBucketServerMap(clientBucketConfig.UseSsl),
                    BucketConfig.Rev,
                    BucketConfig.Name);

                Interlocked.Exchange(ref KeyMapper, vBucketKeyMapper);
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
        void UpdateServices(Dictionary<IPEndPoint, IServer> servers)
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

            var newSearchNodes = servers
                .Where(x => x.Value.IsSearchNode)
                .Select(x => x.Value)
                .ToList();
            Interlocked.Exchange(ref SearchNodes, newSearchNodes);
            IsSearchCapable = SearchNodes.Count > 0;

            var analyticsNodes = servers
                .Where(x => x.Value.IsAnalyticsNode)
                .Select(x => x.Value)
                .ToList();
            Interlocked.Exchange(ref AnalyticsNodes, analyticsNodes);
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
        public ConcurrentDictionary<string, QueryPlan> QueryCache { get; }
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
