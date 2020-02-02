using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.Operations;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterContext : IDisposable
    {
        private readonly ILogger<ClusterContext> _logger;
        private readonly IConfigHandler _configHandler;
        private readonly IClusterNodeFactory _clusterNodeFactory;
        private readonly CancellationTokenSource _tokenSource;
        protected readonly ConcurrentDictionary<string, IBucket> Buckets = new ConcurrentDictionary<string, IBucket>();
        private bool _disposed;

        //For testing
        public ClusterContext() : this(null, new CancellationTokenSource(), new ClusterOptions())
        {
        }

        public ClusterContext(CancellationTokenSource tokenSource, ClusterOptions options)
            : this(null, tokenSource, options)
        {
        }

        public ClusterContext(ICluster cluster, CancellationTokenSource tokenSource, ClusterOptions options)
        {
            Cluster = cluster;
            ClusterOptions = options;
            _tokenSource = tokenSource;

            // Register this instance of ClusterContext
            options.AddSingletonService(this);

            // Register the ClusterOptions
            options.AddSingletonService(options);

            ServiceProvider = options.BuildServiceProvider();

            _logger = ServiceProvider.GetRequiredService<ILogger<ClusterContext>>();
            _configHandler = ServiceProvider.GetRequiredService<IConfigHandler>();
            _clusterNodeFactory = ServiceProvider.GetRequiredService<IClusterNodeFactory>();
        }

        internal ConcurrentDictionary<IPEndPoint, IClusterNode> Nodes { get; set; } = new ConcurrentDictionary<IPEndPoint, IClusterNode>();

        public ClusterOptions ClusterOptions { get; }

        /// <summary>
        /// <seealso cref="IServiceProvider"/> for dependency injection within the context of this cluster.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        public BucketConfig GlobalConfig { get; set; }

        public ICluster Cluster { get; private set; }

        public bool SupportsCollections { get; private set; }

        public CancellationToken CancellationToken => _tokenSource.Token;

        public void StartConfigListening()
        {
            _configHandler.Start(_tokenSource);
            if (ClusterOptions.EnableConfigPolling) _configHandler.Poll(_tokenSource.Token);
        }

        public void RegisterBucket(BucketBase bucket)
        {
            if (Buckets.TryAdd(bucket.Name, bucket))
            {
                _configHandler.Subscribe(bucket);
            }
        }

        public void UnRegisterBucket(BucketBase bucket)
        {
            if (Buckets.TryRemove(bucket.Name, out var removedBucket))
            {
                _configHandler.Unsubscribe(bucket);
                removedBucket.Dispose();
            }
        }

        public void PublishConfig(BucketConfig bucketConfig)
        {
            _configHandler.Publish(bucketConfig);
        }

        public IClusterNode GetRandomNodeForService(ServiceType service, string bucketName = null)
        {
            IClusterNode node;
            switch (service)
            {
                case ServiceType.Views:
                    try
                    {
                        node = Nodes.Values.GetRandom(x => x.HasViews && x.Owner
                                                           != null && x.Owner.Name == bucketName);
                    }
                    catch (NullReferenceException e)
                    {
                        throw new ServiceMissingException(
                            $"No node with the Views service has been located for {bucketName}");
                    }

                    break;
                case ServiceType.Query:
                    node = Nodes.Values.GetRandom(x => x.HasQuery);
                    break;
                case ServiceType.Search:
                    node = Nodes.Values.GetRandom(x => x.HasSearch);
                    break;
                case ServiceType.Analytics:
                    node = Nodes.Values.GetRandom(x => x.HasAnalytics);
                    break;
                default:
                    throw new ServiceNotAvailableException(service);
            }

            if (node == null)
            {
                throw new ServiceNotAvailableException(service);
            }

            return node;
        }

        public void PruneNodes(BucketConfig config)
        {
            var removed = Nodes.Where(x =>
                !config.NodesExt.Any(y => x.Key.Equals(y.GetIpEndPoint(ClusterOptions))));

            foreach (var node in removed)
            {
                RemoveNode(node.Value);
            }
        }

        public IEnumerable<IClusterNode> GetNodes(string bucketName)
        {
            return Nodes.Values.Where(x => x.Owner != null &&
                                           x.Owner.Name.Equals(bucketName))
                .Select(node => node);
        }

        public IClusterNode GetRandomNode()
        {
            return Nodes.GetRandom().Value;
        }

        public void AddNode(IClusterNode node)
        {
            if (Nodes.TryAdd(node.EndPoint, node))
            {
                _logger.LogDebug("Added {0}", node.EndPoint);
            }
        }

        public bool RemoveNode(IClusterNode removedNode)
        {
            if (Nodes.TryRemove(removedNode.EndPoint, out removedNode))
            {
                _logger.LogDebug("Removing {0}", removedNode.EndPoint);
                removedNode.Dispose();
                removedNode = null;
                return true;
            }
            return false;
        }

        public void RemoveNodes()
        {
            foreach (var clusterNode in Nodes)
            {
                RemoveNode(clusterNode.Value);
            }
        }

        public bool NodeExists(IClusterNode node)
        {
            var found = Nodes.ContainsKey(node.EndPoint);
            _logger.LogDebug(found ? "Found {0}" : "Did not find {0}", node.EndPoint);
            return found;
        }

        public bool TryGetNode(IPEndPoint endPoint, out IClusterNode node)
        {
            return Nodes.TryGetValue(endPoint, out node);
        }

        public IClusterNode GetUnassignedNode(Uri uri, bool useIp6Address)
        {
            return Nodes.Values.FirstOrDefault(
                x => !x.IsAssigned && x.EndPoint.Address.Equals(uri.GetIpAddress(useIp6Address)));
        }

        public async Task InitializeAsync()
        {
            // DNS-SRV
            if (ClusterOptions.IsValidDnsSrv())
            {
                try
                {
                    var dnsResolver = ServiceProvider.GetRequiredService<IDnsResolver>();

                    var bootstrapUri = ClusterOptions.ConnectionStringValue.GetDnsBootStrapUri();
                    var servers = (await dnsResolver.GetDnsSrvEntriesAsync(bootstrapUri)).ToList();
                    if (servers.Any())
                    {
                        _logger.LogInformation($"Successfully retrieved DNS SRV entries: [{string.Join(",", servers)}]");
                        ClusterOptions.Servers(servers);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogInformation(exception, "Error trying to retrieve DNS SRV entries.");
                }
            }

            foreach (var server in ClusterOptions.ServersValue)
            {
                var bsEndpoint = server.GetIpEndPoint(ClusterOptions.KvPort, ClusterOptions.EnableIPV6Addressing);
                var node = await _clusterNodeFactory.CreateAndConnectAsync(bsEndpoint);
                node.BootstrapUri = server;
                GlobalConfig = await node.GetClusterMap();

                if (GlobalConfig == null) //TODO NCBC-1966 xerror info is being hidden, so on failure this will not be null
                {
                    AddNode(node); //GCCCP is not supported - pre-6.5 server fall back to CCCP like SDK 2
                }
                else
                {
                    GlobalConfig.IsGlobal = true;
                    foreach (var nodeAdapter in GlobalConfig.GetNodes())//Initialize cluster nodes for global services
                    {
                        if (server.Host.Equals(nodeAdapter.Hostname))//this is the bootstrap node so update
                        {
                            node.BootstrapUri = server;
                            node.NodesAdapter = nodeAdapter;
                            node.BuildServiceUris();
                            SupportsCollections = node.Supports(ServerFeatures.Collections);
                            AddNode(node);
                        }
                        else
                        {
                            var endpoint = nodeAdapter.GetIpEndPoint(ClusterOptions.EnableTls);
                            if (endpoint.Port == 0) endpoint.Port = 11210;
                            var newNode = await _clusterNodeFactory.CreateAndConnectAsync(endpoint);
                            newNode.BootstrapUri = server;
                            newNode.NodesAdapter = nodeAdapter;
                            newNode.BuildServiceUris();
                            SupportsCollections = node.Supports(ServerFeatures.Collections);
                            AddNode(newNode);
                        }
                    }
                }
            }
        }

        public async Task<IBucket> GetOrCreateBucketAsync(string name)
        {
            if (Buckets.TryGetValue(name, out var bucket))
            {
                return bucket;
            }

            foreach (var server in ClusterOptions.ServersValue)
            {
                foreach (var type in Enum.GetValues(typeof(BucketType)))
                {
                    bucket = await CreateAndBootStrapBucketAsync(name, server, (BucketType) type);
                    return bucket;
                }
            }

            return bucket;
        }

        public async Task<IBucket> CreateAndBootStrapBucketAsync(string name, Uri uri, BucketType type)
        {
            var node = GetUnassignedNode(uri, ClusterOptions.EnableIPV6Addressing);
            if (node == null)
            {
                var endpoint = uri.GetIpEndPoint(ClusterOptions.KvPort, ClusterOptions.UseInterNetworkV6Addresses);
                node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint);
                node.BootstrapUri = uri;
                AddNode(node);
            }

            var bucketFactory = ServiceProvider.GetRequiredService<IBucketFactory>();
            var bucket = bucketFactory.Create(name, type);

            try
            {
                await bucket.BootstrapAsync(node);
                RegisterBucket(bucket);
            }
            catch(Exception e)
            {
                _logger.LogError(e, $"Could not bootstrap bucket {type}/{name}");
                UnRegisterBucket(bucket);
            }
            return bucket;
        }

        public async Task ProcessClusterMapAsync(IBucket bucket, BucketConfig config)
        {
            foreach (var nodeAdapter in config.GetNodes())
            {
                var endPoint = nodeAdapter.GetIpEndPoint(ClusterOptions.EnableTls);
                if (TryGetNode(endPoint, out IClusterNode bootstrapNode))
                {
                    _logger.LogDebug($"Using existing node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}");
                    await bootstrapNode.SelectBucket(bucket.Name);
                    bootstrapNode.NodesAdapter = nodeAdapter;
                    bootstrapNode.BuildServiceUris();
                    SupportsCollections = bootstrapNode.Supports(ServerFeatures.Collections);
                    continue; //bootstrap node is skipped because it already went through these steps
                }

                _logger.LogDebug($"Creating node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}");
                var node = await _clusterNodeFactory.CreateAndConnectAsync(endPoint);
                node.Owner = bucket;
                await node.SelectBucket(bucket.Name);
                node.NodesAdapter = nodeAdapter;
                node.BuildServiceUris();
                SupportsCollections = node.Supports(ServerFeatures.Collections);
                AddNode(node);
            }

            PruneNodes(config);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _configHandler?.Dispose();
            _tokenSource?.Dispose();

            foreach (var bucketName in Buckets.Keys)
            {
                if (Buckets.TryRemove(bucketName, out var bucket))
                {
                    bucket.Dispose();
                }
            }

            foreach (var endpoint in Nodes.Keys)
            {
                if (Nodes.TryRemove(endpoint, out var node))
                {
                    node.Dispose();
                }
            }
        }
    }
}
