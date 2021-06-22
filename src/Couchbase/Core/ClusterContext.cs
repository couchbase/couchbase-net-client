using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Couchbase.Core
{
    internal class ClusterContext : IDisposable
    {
        public readonly ITypeTranscoder GlobalTranscoder = new JsonTranscoder();
        private readonly ILogger<ClusterContext> _logger;
        private readonly IRedactor _redactor;
        private readonly IConfigHandler _configHandler;
        private readonly IClusterNodeFactory _clusterNodeFactory;
        private readonly CancellationTokenSource _tokenSource;
        protected readonly ConcurrentDictionary<string, BucketBase> Buckets = new ConcurrentDictionary<string, BucketBase>();
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
            options.AddClusterService(this);

            ServiceProvider = options.BuildServiceProvider();

            _logger = ServiceProvider.GetRequiredService<ILogger<ClusterContext>>();
            _redactor = ServiceProvider.GetRequiredService<IRedactor>();
            _configHandler = ServiceProvider.GetRequiredService<IConfigHandler>();
            _clusterNodeFactory = ServiceProvider.GetRequiredService<IClusterNodeFactory>();
        }

        /// <summary>
        /// Nodes currently being managed.
        /// </summary>
        public ClusterNodeCollection Nodes { get; } = new ClusterNodeCollection();

        public ClusterOptions ClusterOptions { get; }

        /// <summary>
        /// <seealso cref="IServiceProvider"/> for dependency injection within the context of this cluster.
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        public BucketConfig GlobalConfig { get; set; }

        public bool IsGlobal => GlobalConfig != null && GlobalConfig.IsGlobal;

        public ICluster Cluster { get; }

        public bool SupportsCollections { get; set; }

        public bool SupportsGlobalConfig { get; private set; }

        public bool SupportsPreserveTtl { get; internal set; }

        public CancellationToken CancellationToken => _tokenSource.Token;

        public void StartConfigListening()
        {
            _configHandler.Start(ClusterOptions.EnableConfigPolling);
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
            _logger.LogDebug(LoggingEvents.ConfigEvent, JsonConvert.SerializeObject(bucketConfig));
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
                        node = Nodes.GetRandom(x => x.HasViews && x.Owner
                                                           != null && x.Owner.Name == bucketName);
                    }
                    catch (NullReferenceException)
                    {
                        throw new ServiceMissingException(
                            $"No node with the Views service has been located for {_redactor.MetaData(bucketName)}");
                    }

                    break;
                case ServiceType.Query:
                    node = Nodes.GetRandom(x => x.HasQuery);
                    break;
                case ServiceType.Search:
                    node = Nodes.GetRandom(x => x.HasSearch);
                    break;
                case ServiceType.Analytics:
                    node = Nodes.GetRandom(x => x.HasAnalytics);
                    break;
                default:
                    _logger.LogDebug("No nodes available for service {service}", service);
                    throw new ServiceNotAvailableException(service);
            }

            if (node == null)
            {
                _logger.LogDebug("Could not lookup node for service {service}.", service);

                foreach (var node1 in Nodes)
                {
                    _logger.LogDebug("Using node owned by {bucket} using revision {endpoint}", node1.Owner?.Name, node1.EndPoint);
                }
                throw new ServiceNotAvailableException(service);
            }

            return node;
        }

        public IEnumerable<IClusterNode> GetNodes(string bucketName)
        {
            //global nodes
            if (bucketName == null)
            {
                return Nodes;
            }

            //bucket owned nodes
            return Nodes.Where(x => x.Owner != null && x.Owner.Name.Equals(bucketName))
                .Select(node => node);
        }

        public IClusterNode GetRandomNode()
        {
            return Nodes.GetRandom();
        }

        public void AddNode(IClusterNode node)
        {
            _logger.LogDebug("Adding node {endPoint} to {nodes}.", _redactor.SystemData(node.EndPoint), Nodes);
            if (Nodes.Add(node))
            {
                _logger.LogDebug("Added node {endPoint} to {nodes}", _redactor.SystemData(node.EndPoint), Nodes);
            }
        }

        public bool RemoveNode(IClusterNode removedNode)
        {
            _logger.LogDebug("Removing node {endPoint} from {nodes}.", _redactor.SystemData(removedNode.EndPoint), Nodes);
            if (Nodes.Remove(removedNode.EndPoint, out removedNode))
            {
                _logger.LogDebug("Removed node {endPoint} from {nodes}", _redactor.SystemData(removedNode.EndPoint), Nodes);
                removedNode.Dispose();
                return true;
            }
            return false;
        }

        public void RemoveAllNodes()
        {
            foreach (var removedNode in Nodes.Clear())
            {
                removedNode.Dispose();
            }
        }

        public void RemoveAllNodes(IBucket bucket)
        {
            foreach (var removedNode in Nodes.Clear(bucket))
            {
                removedNode.Dispose();
            }
        }

        public IClusterNode GetUnassignedNode(HostEndpoint endpoint, BucketType bucketType)
        {
            return Nodes.FirstOrDefault(
                x => !x.IsAssigned && x.BootstrapEndpoint.Equals(endpoint) && x.BucketType == bucketType);
        }

        public async Task BootstrapGlobalAsync()
        {
            if (ClusterOptions.ConnectionStringValue == null)
            {
                throw new InvalidOperationException("ConnectionString has not been set.");
            }

            if (ClusterOptions.ConnectionStringValue.IsValidDnsSrv())
            {
                try
                {
                    // Always try to use DNS SRV to bootstrap if connection string is valid
                    // It can be disabled by returning an empty URI list from IDnsResolver
                    var dnsResolver = ServiceProvider.GetRequiredService<IDnsResolver>();

                    var bootstrapUri = ClusterOptions.ConnectionStringValue.GetDnsBootStrapUri();
                    var servers = (await dnsResolver.GetDnsSrvEntriesAsync(bootstrapUri, CancellationToken).ConfigureAwait(false)).ToList();
                    if (servers.Any())
                    {
                        _logger.LogInformation(
                            $"Successfully retrieved DNS SRV entries: [{_redactor.SystemData(string.Join(",", servers))}]");
                        ClusterOptions.ConnectionStringValue =
                            new ConnectionString(ClusterOptions.ConnectionStringValue, servers);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogInformation(exception, "Error trying to retrieve DNS SRV entries.");
                }
            }

            //Try to bootstrap each node in the servers list - either from DNS-SRV lookup or from client configuration
            foreach (var server in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions.EnableTls))
            {
                IClusterNode node = null;
                try
                {
                    _logger.LogDebug("Bootstrapping with node {server}", server.Host);
                    node = await _clusterNodeFactory
                        .CreateAndConnectAsync(server, BucketType.Couchbase, CancellationToken)
                        .ConfigureAwait(false);

                    GlobalConfig = await node.GetClusterMap().ConfigureAwait(false);
                    GlobalConfig.SetEffectiveNetworkResolution(server, ClusterOptions);
                }
                catch (CouchbaseException e)
                {
                    if (e.Context is KeyValueErrorContext ctx)
                    {
                        if (ctx.Status == ResponseStatus.BucketNotConnected)
                        {
                            AddNode(node); //GCCCP is not supported - pre-6.5 server fall back to CCCP like SDK 2
                            return;
                        }
                    }
                }
                catch (Exception e)
                {
                    //something else failed, try the next hostname
                    _logger.LogDebug(e, "Attempted bootstrapping on endpoint {endpoint} has failed.", server.Host);
                    continue;
                }

                try
                {
                    //Server is 6.5 and greater and supports GC3P so loop through the global config and
                    //create the nodes that are not associated with any buckets via Select Bucket.
                    GlobalConfig.IsGlobal = true;
                    foreach (var nodeAdapter in GlobalConfig.GetNodes()) //Initialize cluster nodes for global services
                    {
                        //log any alternate address mapping
                        _logger.LogInformation(nodeAdapter.ToString());

                        if (server.Host.Equals(nodeAdapter.Hostname)) //this is the bootstrap node so update
                        {
                            node.NodesAdapter = nodeAdapter;
                            SupportsCollections = node.ServerFeatures.Collections;
                            SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                            AddNode(node);
                        }
                        else
                        {
                            var hostEndpoint = HostEndpoint.Create(nodeAdapter, ClusterOptions);
                            var newNode = await _clusterNodeFactory
                                .CreateAndConnectAsync(hostEndpoint, BucketType.Couchbase, nodeAdapter,
                                    CancellationToken).ConfigureAwait(false);
                            SupportsCollections = node.ServerFeatures.Collections;
                            SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                            AddNode(newNode);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogDebug("Attempted bootstrapping on endpoint {endpoint} has failed.", e, server);
                }
            }
        }

        public async ValueTask<IBucket> GetOrCreateBucketAsync(string name)
        {
            if (Buckets.TryGetValue(name, out var bucket))
            {
                return bucket;
            }

            foreach (var server in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions.EnableTls))
            {
                foreach (var type in Enum.GetValues(typeof(BucketType)))
                {
                    try
                    {
                        bucket = await CreateAndBootStrapBucketAsync(name, server, (BucketType) type)
                            .ConfigureAwait(false);

                        if ((bucket is Bootstrapping.IBootstrappable bootstrappable) && bootstrappable.IsBootstrapped)
                            return bucket;
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation(LoggingEvents.BootstrapEvent, e,
                            "Cannot bootstrap bucket {name} as {type}.", name, type);
                    }
                }
            }

            throw new BucketNotFoundException(name);
        }

        public async Task<BucketBase> CreateAndBootStrapBucketAsync(string name, HostEndpoint endpoint, BucketType type)
        {
            var bucketFactory = ServiceProvider.GetRequiredService<IBucketFactory>();
            var bucket = bucketFactory.Create(name, type);

            var node = GetUnassignedNode(endpoint, type);
            if (node == null)
            {
                node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint, type, CancellationToken).ConfigureAwait(false);
                node.Owner = bucket;
                AddNode(node);
            }

            try
            {
                await bucket.BootstrapAsync(node).ConfigureAwait(false);

                if ((bucket is Bootstrapping.IBootstrappable bootstrappable) && bootstrappable.IsBootstrapped)
                    RegisterBucket(bucket);
            }
            catch(Exception e)
            {
                _logger.LogError(e, "Could not bootstrap bucket {name}.", _redactor.MetaData(name));
                RemoveAllNodes(bucket);
                UnRegisterBucket(bucket);
                await bucket.DisposeAsync().ConfigureAwait(false);
                throw;
            }
            return bucket;
        }

        public async Task RebootStrapAsync(string name)
        {
            if(Buckets.TryGetValue(name, out var bucket))
            {
                //need to remove the old nodes
                var oldNodes = Nodes.Where(x => x.Owner == bucket).ToArray();
                foreach (var node in oldNodes)
                {
                    if (Nodes.Remove(node.EndPoint, out var removedNode))
                    {
                        removedNode.Dispose();
                    }
                }

                //start going through the bootstrap list trying to connect
                foreach (var endpoint in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions
                    .EnableTls))
                {
                    var node = GetUnassignedNode(endpoint, BucketType.Couchbase);
                    if (node == null)
                    {
                        node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint, BucketType.Couchbase, CancellationToken)
                            .ConfigureAwait(false);
                        AddNode(node);
                    }

                    try
                    {
                        //connected so let bootstrapping continue on the bucket
                        await bucket.BootstrapAsync(node).ConfigureAwait(false);
                        RegisterBucket(bucket);
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Could not bootstrap bucket {name}.", _redactor.MetaData(name));
                        UnRegisterBucket(bucket);
                    }
                }
            }
            else
            {
                throw new BucketNotFoundException(name);
            }
        }

        public async Task ProcessClusterMapAsync(BucketBase bucket, BucketConfig config)
        {
            var ipEndPointService = ServiceProvider.GetRequiredService<IIpEndPointService>();
            foreach (var nodeAdapter in config.GetNodes())
            {
                //log any alternate address mapping
                _logger.LogInformation(nodeAdapter.ToString());

                var endPoint = await ipEndPointService.GetIpEndPointAsync(nodeAdapter, CancellationToken).ConfigureAwait(false);
                if (Nodes.TryGet(endPoint, out var bootstrapNode))
                {
                    if (bootstrapNode.Owner == null && bucket.BucketType != BucketType.Memcached)
                    {
                        _logger.LogDebug(
                            "Using existing node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}",
                            _redactor.SystemData(endPoint), _redactor.MetaData(bucket.Name), config.Rev);

                        if (bootstrapNode.HasKv)
                        {
                            await bootstrapNode.SelectBucketAsync(bucket, CancellationToken).ConfigureAwait(false);
                            SupportsCollections = bootstrapNode.ServerFeatures.Collections;
                            SupportsPreserveTtl = bootstrapNode.ServerFeatures.PreserveTtl;
                        }

                        bootstrapNode.Owner = bucket;
                        bootstrapNode.NodesAdapter = nodeAdapter;
                        bucket.Nodes.Add(bootstrapNode);
                        continue;
                    }
                    if (bootstrapNode.Owner != null && bootstrapNode.BucketType == BucketType.Memcached)
                    {
                        _logger.LogDebug("Adding memcached node for endpoint {endpoint} using rev#{revision} for bucket {bucketName}.", _redactor.SystemData(endPoint), config.Rev, _redactor.MetaData(config.Name));
                        bootstrapNode.NodesAdapter = nodeAdapter;
                        bucket.Nodes.Add(bootstrapNode);
                        continue;
                    }
                }

                //If the node already exists for the endpoint, ignore it.
                if (bucket.Nodes.TryGet(endPoint, out var bucketNode))
                {
                    _logger.LogDebug("The node already exists for the endpoint {endpoint} using rev#{revision} for bucket {bucketName}.", _redactor.SystemData(endPoint), config.Rev, _redactor.MetaData(config.Name));
                    bucketNode.NodesAdapter = nodeAdapter;
                    continue;
                }

                _logger.LogDebug("Creating node {endPoint} for bucket {bucketName} using rev#{revision}",
                    _redactor.SystemData(endPoint), _redactor.MetaData(bucket.Name), config.Rev);

                var bucketType = config.NodeLocator == "ketama" ? BucketType.Memcached : BucketType.Couchbase;
                var node = await _clusterNodeFactory.CreateAndConnectAsync(
                    // We want the BootstrapEndpoint to use the host name, not just the IP
                    new HostEndpoint(nodeAdapter.Hostname, endPoint.Port),
                    bucketType,
                    nodeAdapter,
                    CancellationToken).ConfigureAwait(false);

                if (node.HasKv)
                {
                    await node.SelectBucketAsync(bucket, CancellationToken).ConfigureAwait(false);
                    SupportsCollections = node.ServerFeatures.Collections;
                    SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                }

                AddNode(node);
                bucket.Nodes.Add(node);//may remove
            }

            await PruneNodesAsync(config).ConfigureAwait(false);
        }

        public async Task PruneNodesAsync(BucketConfig config)
        {
            var ipEndpointService = ServiceProvider.GetRequiredService<IIpEndPointService>();

            var existingEndpoints = await config.GetNodes().ToAsyncEnumerable()
                .SelectAwait(p => ipEndpointService.GetIpEndPointAsync(p, CancellationToken))
                .ToListAsync(CancellationToken).ConfigureAwait(false);

            _logger.LogDebug("ExistingEndpoints: {endpoints}, revision {revision}.", existingEndpoints, config.Rev);

            var removedEndpoints = Nodes.Where(x =>
                !existingEndpoints.Any(y => x.KeyEndPoints.Any(z => z.Address.Equals(y.Address))));

            _logger.LogDebug("RemovedEndpoints: {endpoints}, revision {revision}", removedEndpoints, config.Rev);

            foreach (var node in removedEndpoints)
            {
                RemoveNode(node);
            }
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

            RemoveAllNodes();
        }
    }
}
