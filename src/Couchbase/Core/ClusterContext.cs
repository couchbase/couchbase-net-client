using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase.Core
{
    internal class ClusterContext : IDisposable
    {
        private static readonly ILogger Log = LogManager.CreateLogger<ClusterContext>();
        private readonly ConfigHandler _configHandler;
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
            _configHandler = new ConfigHandler(this);//TODO make injectable
        }

        internal ConcurrentDictionary<IPEndPoint, IClusterNode> Nodes { get; set; } = new ConcurrentDictionary<IPEndPoint, IClusterNode>();

        public ClusterOptions ClusterOptions { get; }

        public BucketConfig GlobalConfig { get; set; }

        public ICluster Cluster { get; private set; }

        public bool SupportsCollections { get; private set; }

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
                Log.LogDebug("Added {0}", node.EndPoint);
            }
        }

        public bool RemoveNode(IClusterNode removedNode)
        {
            if (Nodes.TryRemove(removedNode.EndPoint, out removedNode))
            {
                Log.LogDebug("Removing {0}", removedNode.EndPoint);
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
            Log.LogDebug(found ? "Found {0}" : "Did not find {0}", node.EndPoint);
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
                    var bootstrapUri = ClusterOptions.ConnectionString.GetDnsBootStrapUri();
                    var servers = await ClusterOptions.DnsResolver.GetDnsSrvEntriesAsync(bootstrapUri);
                    if (servers.Any())
                    {
                        Log.LogInformation($"Successfully retrieved DNS SRV entries: [{string.Join(",", servers)}]");
                        ClusterOptions.WithServers(servers);
                    }
                }
                catch (Exception exception)
                {
                    Log.LogInformation(exception, "Error trying to retrieve DNS SRV entries.");
                }
            }

            foreach (var server in ClusterOptions.Servers)
            {
                var bsEndpoint = server.GetIpEndPoint(ClusterOptions.KvPort, ClusterOptions.EnableIPV6Addressing);
                var node = await ClusterNode.CreateAsync(this, bsEndpoint);
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
                            var newNode = await ClusterNode.CreateAsync(this, endpoint);
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

            foreach (var server in ClusterOptions.Servers)
            {
                foreach (var type in Enum.GetValues(typeof(BucketType)))
                {
                    try
                    {
                        bucket = await BootstrapBucketAsync(name, server, (BucketType) type);
                        RegisterBucket((BucketBase)bucket);
                        return bucket;
                    }
                    catch (Exception e)
                    {
                        Log.LogWarning(e, $"Could not bootstrap {type} {name}.");
                    }
                }
            }
            throw new AuthenticationFailureException();
        }

        public async Task<IBucket> BootstrapBucketAsync(string name, Uri uri, BucketType type)
        {
            var node = GetUnassignedNode(uri, ClusterOptions.EnableIPV6Addressing);
            if (node == null)
            {
                var endpoint = uri.GetIpEndPoint(ClusterOptions.KvPort, ClusterOptions.UseInterNetworkV6Addresses);
                node = await ClusterNode.CreateAsync(this, endpoint);
                node.BootstrapUri = uri;
                AddNode(node);
            }

            BucketBase bucket = null;
            switch (type)
            {
                case BucketType.Couchbase:
                case BucketType.Ephemeral:
                    bucket = new CouchbaseBucket(name, this);
                    break;
                case BucketType.Memcached:
                    bucket = new MemcachedBucket(name, this);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }

            try
            {
                await bucket.BootstrapAsync(node);
                RegisterBucket(bucket);
                return bucket;
            }
            catch(Exception e)
            {
                Log.LogError(e, $"Could not bootstrap {name}");
                UnRegisterBucket(bucket);
                throw;
            }
        }

        public async Task ProcessClusterMapAsync(IBucket bucket, BucketConfig config)
        {
            foreach (var nodeAdapter in config.GetNodes())
            {
                var endPoint = nodeAdapter.GetIpEndPoint(ClusterOptions.EnableTls);
                if (TryGetNode(endPoint, out IClusterNode bootstrapNode))
                {
                    Log.LogDebug($"Using existing node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}");
                    await bootstrapNode.SelectBucket(bucket.Name);
                    bootstrapNode.NodesAdapter = nodeAdapter;
                    bootstrapNode.BuildServiceUris();
                    SupportsCollections = bootstrapNode.Supports(ServerFeatures.Collections);
                    continue; //bootstrap node is skipped because it already went through these steps
                }

                Log.LogDebug($"Creating node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}");
                var node = await ClusterNode.CreateAsync(this, endPoint);
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
