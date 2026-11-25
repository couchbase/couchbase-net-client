using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Metrics;
using Couchbase.Core.Diagnostics.Metrics.AppTelemetry;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Diagnostics.Tracing.OrphanResponseReporting;
using Couchbase.Core.Diagnostics.Tracing.ThresholdTracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.Core.RateLimiting;
using Couchbase.Management.Buckets;
using Couchbase.Utils;
using DnsClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Core
{
    internal class ClusterContext : IDisposable, IConfigUpdateEventSink
    {
        /// <summary>
        /// Transcoder for use on internal key/value operations.
        /// </summary>
        /// <remarks>
        /// This transcoder will only function for serializing and deserializing types registered on
        /// <see cref="InternalSerializationContext"/>. Trying to use any other type will throw an exception.
        /// </remarks>
        public readonly ITypeTranscoder GlobalTranscoder =
            new JsonTranscoder(SystemTextJsonSerializer.Create(InternalSerializationContext.Default));

        private readonly ClusterOptions _clusterOptions;
        private readonly ILogger<ClusterContext> _logger;
        private readonly Redactor _redactor;
        private readonly IConfigHandler _configHandler;
        private readonly IClusterNodeFactory _clusterNodeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly CancellationTokenSource _tokenSource;
        protected readonly ConcurrentDictionary<string, BucketBase> Buckets = new();
        private int _disposed;
        private readonly object _rebootstrapLock = new();
        private long? _lastRebootstrap;
        private BucketConfig _clusterCapsConfig;
        private BucketConfig _clusterNodesConfig;
        private BucketConfig _reconciledClusterNodesConfig;
        private readonly SemaphoreSlim _semaphore = new(1);
        private readonly HttpClusterMapBase _httpClusterMap;
        private readonly IHttpClusterMapFactory _httpClusterMapFactory;

        // Maintains a list of objects to be disposed when the context is disposed.
        private readonly List<IDisposable> _ownedObjects = new();

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
            _clusterOptions = options;

            // Register this instance of ClusterContext
            options.AddClusterService(this);

            ServiceProvider = options.BuildServiceProvider();

            _logger = ServiceProvider.GetRequiredService<ILogger<ClusterContext>>();
            _redactor = ServiceProvider.GetRequiredService<Redactor>();
            _configHandler = ServiceProvider.GetRequiredService<IConfigHandler>();
            GlobalConfigRefresher = ServiceProvider.GetRequiredService<IConfigRefresherFactory>().CreateGlobalRefresher(this);
            _clusterNodeFactory = ServiceProvider.GetRequiredService<IClusterNodeFactory>();
            _timeProvider = ServiceProvider.GetRequiredService<TimeProvider>();
            _httpClusterMapFactory = ServiceProvider.GetRequiredService<IHttpClusterMapFactory>();
            _httpClusterMap = _httpClusterMapFactory.Create(this);

            // Dispose per-cluster meters (like LoggingMeter) when the cluster is disposed.
            var meter = ServiceProvider.GetRequiredService<IMeter>();
            if (meter is IDisposable disposableMeter && meter is not NoopMeter)
            {
                _ownedObjects.Add(disposableMeter);
            }
        }

        /// <summary>
        /// The cluster nodes. Each has no bucket and follows <see cref="ClusterNodesConfig"/>. The nodes of a bucket
        /// live in <see cref="BucketBase.Nodes"/> only.
        /// </summary>
        public ClusterNodeList Nodes { get; } = new();

        /// <summary>
        /// Every node of the client, the cluster nodes in <see cref="Nodes"/> and the nodes of each open bucket.
        /// </summary>
        /// <remarks>
        /// A bucket appears here once it is registered.
        /// </remarks>
        internal List<IClusterNode> GetAllNodes()
        {
            var nodes = new List<IClusterNode>(Nodes);
            foreach (var bucket in Buckets)
            {
                nodes.AddRange(bucket.Value.Nodes);
            }

            return nodes;
        }

        public ClusterOptions ClusterOptions { get; }

        /// <summary>
        /// <seealso cref="ICouchbaseServiceProvider"/> for dependency injection within the context of this cluster.
        /// </summary>
        public ICouchbaseServiceProvider ServiceProvider { get; }

        public BucketConfig GlobalConfig { get; set; }

        /// <summary>
        /// The newest config from any source that carries cluster capabilities. A bucket config is often newer than
        /// <see cref="GlobalConfig"/>, so it can bring new capabilities first after an upgrade.
        /// </summary>
        internal BucketConfig ClusterCapsConfig => Volatile.Read(ref _clusterCapsConfig);

        /// <summary>
        /// Raised when <see cref="ClusterCapsConfig"/> changes the set of cluster capabilities.
        /// </summary>
        internal event Action ClusterCapsChanged;

        /// <summary>
        /// Keeps <see cref="GlobalConfig"/> up to date over KV.
        /// </summary>
        internal GlobalConfigRefresher GlobalConfigRefresher { get; }

        public bool IsGlobal => GlobalConfig != null && GlobalConfig.IsGlobal;

        internal bool HasOpenBuckets => !Buckets.IsEmpty;

        public ICluster Cluster { get; }

        public bool SupportsGlobalConfig { get; private set; }

        public bool SupportsPreserveTtl { get; internal set; }

        public bool SupportsBinaryXattr { get; internal set; }

        public CancellationToken CancellationToken => _tokenSource.Token;

        /// <summary>
        /// Whether the cluster has the capability, or null while no capabilities are known.
        /// </summary>
        internal bool? HasClusterCap(string clusterCap) => ClusterCapsConfig?.HasClusterCap(clusterCap);

        /// <summary>
        /// Throws if the cluster does not have the capability. Does nothing while no capabilities are known.
        /// </summary>
        internal void AssertClusterCap(string clusterCap, string message = null) =>
            ClusterCapsConfig?.AssertClusterCap(clusterCap, message);

        /// <summary>
        /// Stores the config as <see cref="ClusterCapsConfig"/> if it carries cluster capabilities and is newer,
        /// or always when <paramref name="replace"/> is set.
        /// </summary>
        internal void UpdateClusterCapsConfig(BucketConfig config, bool replace = false)
        {
            // A config with no capabilities must not erase the known ones.
            if (config.ClusterCapabilities is not { Count: > 0 }) return;

            BucketConfig current;
            do
            {
                current = Volatile.Read(ref _clusterCapsConfig);

                // Bucket and cluster configs share one revision space, and lagging nodes send older configs late.
                if (!replace && current is not null && config.ConfigVersion <= current.ConfigVersion) return;
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref _clusterCapsConfig, config, current), current));

            if (current is null || !config.HasSameClusterCaps(current))
            {
                ClusterCapsChanged?.Invoke();
            }
        }

        /// <summary>
        /// The newest config of any kind. The cluster nodes follow its node list, because a bucket config is often
        /// newer than the cluster config.
        /// </summary>
        internal BucketConfig ClusterNodesConfig => Volatile.Read(ref _clusterNodesConfig);

        /// <summary>
        /// Stores the config as <see cref="ClusterNodesConfig"/> if it is newer or forced, or always when
        /// <paramref name="replace"/> is set.
        /// </summary>
        internal void UpdateClusterNodesConfig(BucketConfig config, bool replace = false)
        {
            BucketConfig current;
            do
            {
                current = Volatile.Read(ref _clusterNodesConfig);
                if (!replace && !config.IgnoreRev && current is not null && config.ConfigVersion <= current.ConfigVersion)
                {
                    return;
                }
            }
            while (!ReferenceEquals(Interlocked.CompareExchange(ref _clusterNodesConfig, config, current), current));
        }

        /// <summary>
        /// Whether a config is older than <see cref="ClusterNodesConfig"/>. A forced config is never older.
        /// </summary>
        private bool IsOlderThanClusterNodesConfig(BucketConfig config) =>
            !config.IgnoreRev && ClusterNodesConfig is { } view && config.ConfigVersion < view.ConfigVersion;

        /// <summary>
        /// Whether the cluster nodes already follow this config, so a config at its revision needs no node work.
        /// </summary>
        private bool IsReconciled(BucketConfig view) =>
            ReferenceEquals(Volatile.Read(ref _reconciledClusterNodesConfig), view);

        public void Start()
        {
            var requestTracer = ServiceProvider.GetRequiredService<IRequestTracer>();
            if (requestTracer is not NoopRequestTracer)
            {
                //if tracing is disabled the listener will be ignored
                if (_clusterOptions.ThresholdOptions.Enabled)
                {
                    var listener = _clusterOptions.ThresholdOptions.ThresholdListener;
                    if (listener is null)
                    {
                        listener = new ThresholdTraceListener(
                            ServiceProvider.GetRequiredService<ILoggerFactory>(),
                            _clusterOptions.ThresholdOptions);

                        // Since we own the listener, be sure we dispose it
                        _ownedObjects.Add(listener);
                    }

                    requestTracer.Start(listener);
                }

                //if tracing is disabled the listener will be ignored
                if (_clusterOptions.OrphanTracingOptions.Enabled)
                {
                    var listener = _clusterOptions.OrphanTracingOptions.OrphanListener;
                    if (listener is null)
                    {
                        listener = new OrphanTraceListener(
                            new OrphanReporter(ServiceProvider.GetRequiredService<ILogger<OrphanReporter>>(),
                                _clusterOptions.OrphanTracingOptions));

                        // Since we own the listener, be sure we dispose it
                        _ownedObjects.Add(listener);
                    }

                    requestTracer.Start(listener);
                }
            }

            _configHandler.Start();
        }

        public void RegisterBucket(BucketBase bucket)
        {
            if (Buckets.TryAdd(bucket.Name, bucket))
            {
                _configHandler.Subscribe(bucket);
                RecomputeClusterFeatureSupport();
            }
        }

        public void UnRegisterBucket(BucketBase bucket)
        {
            if (Buckets.TryRemove(bucket.Name, out var removedBucket))
            {
                _configHandler.Unsubscribe(bucket);
                ServiceProvider.GetService<IAppTelemetryCollector>()?.OnConfigRemoved(bucket.Name);
                removedBucket.Dispose();
                RecomputeClusterFeatureSupport();
            }
        }

        public void RemoveBucket(BucketBase bucket)
        {
            if (Buckets.TryRemove(bucket.Name, out _))
            {
                _configHandler.Unsubscribe(bucket);
                ServiceProvider.GetService<IAppTelemetryCollector>()?.OnConfigRemoved(bucket.Name);
                RecomputeClusterFeatureSupport();
            }
        }

        /// <summary>
        /// Feeds a config to App Telemetry. Skips configs of closed buckets, because they can still be queued.
        /// </summary>
        internal void UpdateAppTelemetryConfig(BucketConfig config)
        {
            var collector = ServiceProvider.GetService<IAppTelemetryCollector>();

            // A config with no name comes from a connection with no bucket, so it is the cluster config.
            if (config.Name is null || config.IsGlobal)
            {
                collector?.OnConfigUpdated(config);
                return;
            }

            if (!Buckets.ContainsKey(config.Name)) return;
            collector?.OnConfigUpdated(config);

            // The bucket can close during the update, after RemoveBucket dropped its endpoints. Drop them again.
            if (!Buckets.ContainsKey(config.Name))
            {
                collector?.OnConfigRemoved(config.Name);
            }
        }

        public void PublishConfig(BucketConfig bucketConfig)
        {
            //Ignore any null configs if config deduping is enabled
            if (bucketConfig == null) return;
            _configHandler.Publish(bucketConfig);
        }

        public IClusterNode GetRandomNodeForService(ServiceType service, string bucketName = null)
            => GetRandomNodeForService(service, bucketName, excludedNodes: null);

        public IClusterNode GetRandomNodeForService(ServiceType service, string bucketName, IList<Uri> excludedNodes)
        {
            IClusterNode node;
            switch (service)
            {
                case ServiceType.Views:
                    node = bucketName is not null && Buckets.TryGetValue(bucketName, out var bucket)
                        ? bucket.Nodes.RandomOrDefault(static x => x.HasViews)
                        : null;
                    break;
                case ServiceType.Query:
                    node = GetNodeWithExclusion(static x => x.HasQuery, static x => x.QueryUri, excludedNodes);
                    break;
                case ServiceType.Search:
                    node = GetNodeWithExclusion(static x => x.HasSearch, static x => x.SearchUri, excludedNodes);
                    break;
                case ServiceType.Analytics:
                    node = GetNodeWithExclusion(static x => x.HasAnalytics, static x => x.AnalyticsUri, excludedNodes);
                    break;
                case ServiceType.Eventing:
                    node = Nodes.RandomOrDefault(static x => x.HasEventing);
                    break;
                case ServiceType.Management:
                    node = Nodes.RandomOrDefault(static x => x.ManagementUri != null);
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
                    _logger.LogDebug("Cluster node {endpoint}", _redactor.SystemData(node1.EndPoint));
                }

                throw new ServiceNotAvailableException(service);
            }

            // If a node was found but the server is an Enterprise Analytics cluster, throw
            if (!_clusterOptions.Experiments.EnableEnterpriseAnalytics && service is ServiceType.Analytics)
            {
                ThrowHelper.ThrowIfIsEnterpriseAnalytics(GlobalConfig.Product);
            }

            return node;
        }

        /// <summary>
        /// Selects a random node for the given service, excluding any nodes whose service URI
        /// is in the exclusion list. Falls back to any matching node if all are excluded.
        /// </summary>
        private IClusterNode GetNodeWithExclusion(
            Func<IClusterNode, bool> hasService,
            Func<IClusterNode, Uri> getUri,
            IList<Uri> excludedNodes)
        {
            if (excludedNodes is not { Count: > 0 })
            {
                return Nodes.RandomOrDefault(hasService);
            }

            // Try to find a node that is not excluded
            var node = Nodes.RandomOrDefault(x => hasService(x) && !excludedNodes.Contains(getUri(x)));

            // If all nodes with this service are excluded, fall back to any node with the service.
            // Better to try a previously-failed node than to throw ServiceNotAvailableException.
            return node ?? Nodes.RandomOrDefault(hasService);
        }

        /// <summary>
        /// The nodes of an open bucket, or every node of the client for the cluster.
        /// </summary>
        public IEnumerable<IClusterNode> GetNodes(string bucketName)
        {
            if (bucketName is null or BucketConfig.GlobalBucketName)
            {
                return GetAllNodes();
            }

            return Buckets.TryGetValue(bucketName, out var bucket) ? bucket.Nodes : [];
        }

        public IClusterNode GetRandomNode()
        {
            var node = Nodes.RandomOrDefault();
            if(node == null)
            {
                ThrowHelper.ThrowServiceNotAvailableException(ServiceType.Management);
            }
            return node;
        }

        public void AddNode(IClusterNode node)
        {
            _logger.LogDebug("Adding node {endPoint} to {nodes}.", _redactor.SystemData(node.EndPoint), Nodes.Count);

            if (Nodes.Add(node))
            {
                _logger.LogDebug("Added node {endPoint} to {nodes}", _redactor.SystemData(node.EndPoint), Nodes.Count);

                RecomputeClusterFeatureSupport();
            }
        }

        /// <summary>
        /// Recomputes the cluster-wide feature flags from every node of the client, see <see cref="GetAllNodes"/>.
        /// </summary>
        /// <remarks>
        /// SupportsPreserveTtl and SupportsBinaryXattr describe the cluster, but they used to be
        /// assigned from whichever single node was initialized last - eight separate sites, each
        /// overwriting the one before, so the answer depended on ordering. A feature is only safe to
        /// use if every node offers it, so this takes the conjunction. Nodes that have not negotiated
        /// yet are skipped rather than counted as unsupporting, so the flags do not flap to false
        /// while a node is still coming up.
        ///
        /// This stays a field rather than a computed property because it is read on every mutation.
        /// </remarks>
        internal void RecomputeClusterFeatureSupport()
        {
            // Cluster nodes finish HELLO in parallel. Without the lock, an older result can be written last.
            lock (_featureSupportLock)
            {
                RecomputeClusterFeatureSupportLocked();
            }
        }

        private void RecomputeClusterFeatureSupportLocked()
        {
            var seenAnyNode = false;
            var preserveTtl = true;
            var binaryXattr = true;

            foreach (var node in GetAllNodes())
            {
                var features = node.ServerFeatures;
                if (features is null)
                {
                    continue;
                }

                seenAnyNode = true;
                preserveTtl &= features.PreserveTtl;
                binaryXattr &= features.SubdocBinaryXattr;
            }

            SupportsPreserveTtl = seenAnyNode && preserveTtl;
            SupportsBinaryXattr = seenAnyNode && binaryXattr;
        }

        public bool RemoveNode(IClusterNode removedNode)
        {
            _logger.LogDebug(
                "Removing node {endPoint} from {nodes}.", _redactor.SystemData(removedNode.EndPoint), Nodes);
            if (Nodes.Remove(removedNode))
            {
                _logger.LogDebug(
                    "Removed node {endPoint} from {nodes}", _redactor.SystemData(removedNode.EndPoint), Nodes);
                removedNode.Dispose();

                //A node leaving can restore a feature the whole cluster now offers.
                RecomputeClusterFeatureSupport();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes and disposes every node of the client, the cluster nodes and the nodes of each open bucket.
        /// </summary>
        public void RemoveAllNodes()
        {
            foreach (var removedNode in Nodes.Clear())
            {
                removedNode.Dispose();
            }

            foreach (var bucket in Buckets)
            {
                foreach (var removedNode in bucket.Value.Nodes.Clear())
                {
                    removedNode.Dispose();
                }
            }

            //The bulk paths clear the list directly, so they must recompute too.
            RecomputeClusterFeatureSupport();
        }

        public async Task BootstrapGlobalAsync(CancellationToken cancellationToken = default)
        {
            if (ClusterOptions.ConnectionStringValue == null)
            {
                ThrowHelper.ThrowInvalidOperationException("ConnectionString has not been set.");
            }

            // Combine the input CancellationToken with the Dispose token, we'll cancel if either is triggered
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken, cancellationToken);

            try
            {
                bool bootstrappedFromDnsSrv = false;
                if (ClusterOptions.ConnectionStringValue.IsValidDnsSrv())
                {
                    try
                    {
                        // Always try to use DNS SRV to bootstrap if connection string is valid
                        // It can be disabled by returning an empty URI list from IDnsResolver
                        var dnsResolver = ServiceProvider.GetRequiredService<IDnsResolver>();

                        //if we have a cached original DNS SRV uri we can use that to bootstrap
                        var bootstrapUri = ClusterOptions.ConnectionStringValue.IsDnsSrv
                            ? ClusterOptions.ConnectionStringValue.DnsSrvUri
                            : ClusterOptions.ConnectionStringValue.GetDnsBootStrapUri();

                        var servers = (await dnsResolver.GetDnsSrvEntriesAsync(bootstrapUri, cts.Token)
                            .ConfigureAwait(false)).ToList();
                        if (servers.Any())
                        {
                            _logger.LogInformation(
                                $"Successfully retrieved DNS SRV entries: [{_redactor.SystemData(string.Join(",", servers))}]");
                            ClusterOptions.ConnectionStringValue =
                                new ConnectionString(ClusterOptions.ConnectionStringValue, servers, true, bootstrapUri);
                            bootstrappedFromDnsSrv = true;
                        }
                        else
                        {
                            _logger.LogInformation(
                                $"Bootstrapping: the DNS SRV succeeded, but no records were returned for {bootstrapUri}.");
                        }
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Don't trap OperationCanceledException if the caller requested cancellation
                        throw;
                    }
                    catch (DnsResponseException) when (cancellationToken.IsCancellationRequested)
                    {
                        // DnsClient wraps OperationCanceledException in a DnsResponseException
                        throw new OperationCanceledException(cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogInformation(exception, "Error trying to retrieve DNS SRV entries.");
                    }
                }

                //defer throwing exceptions until all nodes have been tried.
                //if this is non null that means and we haven't bootstrapped
                //then bootstrapping has failed and we can throw the agg exception
                List<ExceptionDispatchInfo> exceptions = null;

                //Try to bootstrap each node in the servers list - either from DNS-SRV lookup or from client configuration
                foreach (var server in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions
                             .EnableTls))
                {
                    IClusterNode node = null;
                    try
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        _logger.LogDebug("Bootstrapping: global bootstrapping with node {server}", server);
                        node = await _clusterNodeFactory
                            .CreateAndConnectAsync(server, cts.Token)
                            .ConfigureAwait(false);

                        GlobalConfig = await node.GetClusterMap(cancellationToken: cts.Token).ConfigureAwait(false);
                        // Pushes and node views seen before can come from an older cluster behind the same DNS name.
                        GlobalConfigRefresher.ForgetPushedVersions();
                        foreach (var existingNode in GetAllNodes())
                        {
                            existingNode.ForgetNodesAdapterVersion();
                        }
                        GlobalConfig.Name = BucketConfig.GlobalBucketName;
                        GlobalConfig.SetEffectiveNetworkResolution(ClusterOptions);
                        // Bootstrap replaces both, so a DNS SRV move to a cluster with lower revisions takes effect.
                        UpdateClusterCapsConfig(GlobalConfig, replace: true);
                        UpdateClusterNodesConfig(GlobalConfig, replace: true);
                        UpdateAppTelemetryConfig(GlobalConfig);

                        // A server with no cluster config never gets here, so nothing polls for one.
                        GlobalConfigRefresher.Start();

                        //If we are using alt addresses, we likely bootstrapped with a
                        //non-alt port, thus this node cannot be reused. We need to use
                        //the alt-address, so we will recreate this node.
                        if (bootstrappedFromDnsSrv && GlobalConfig.UseAlternateAddresses)
                        {
                            node.Dispose();
                            node = null;
                        }

                        //ignore the exceptions since at least one node bootstrapped
                        exceptions?.Clear();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        // Don't trap OperationCanceledException if the caller requested cancellation
                        node?.Dispose();
                        throw;
                    }
                    catch (Exception e)
                    {
                        if (e is CouchbaseException cbe && cbe.Context is KeyValueErrorContext ctx)
                        {
                            if (ctx.Status == ResponseStatus.BucketNotConnected)
                            {
                                AddNode(node); //GCCCP is not supported - pre-6.5 server fall back to CCCP like SDK 2
                                return;
                            }
                        }

                        // The pool of a seed keeps reconnecting until it is disposed.
                        node?.Dispose();

                        // something else failed, try the next hostname
                        _logger.LogDebug(e,
                            "Bootstrapping: attempted global bootstrapping on endpoint {endpoint} has failed.",
                            server);

                        //hold on to the exception to create agg exception if none complete.
                        (exceptions ??= []).Add(ExceptionDispatchInfo.Capture(e));

                        //skip to next endpoint and try again
                        continue;
                    }

                    try
                    {
                        //Server is 6.5 and greater and supports GC3P. The other cluster nodes join at once and connect
                        //in the background. The seed is connected, so only a dispose stops this step, not the caller.
                        await AddBootstrapNodesAsync(node, CancellationToken).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e)
                    {
                        _logger.LogDebug(e,
                            "Bootstrapping: attempted global bootstrapping on endpoint {endpoint} has failed.",
                            _redactor.MetaData(server));
                    }
                }

                if (exceptions is { Count: > 0 })
                {
                    //for backwards compatibility return an auth exception if one exists (logs will show others).
                    var authException = exceptions.FirstOrDefault(
                        static e => e.SourceException.GetType() == typeof(AuthenticationFailureException));

                    authException?.Throw();

                    //Not an auth exception but still cannot bootstrap so return all the exceptions
                    throw new AggregateException("Bootstrapping has failed!",
                        exceptions.Select(static p => p.SourceException));
                }
            }
            finally
            {
                //subscribe for config updates
                _configHandler.Subscribe(this);
            }
        }

        /// <summary>
        /// Makes the bootstrap seed the cluster node of its entry in <see cref="ClusterNodesConfig"/>, then runs the
        /// cluster reconcile that a config apply runs, under the config lock. The other nodes join at once and connect
        /// in the background.
        /// </summary>
        /// <remarks>
        /// The seed is disposed when no entry has its address, as with a Private Link endpoint or an IP seed. On a
        /// rebootstrap it is also disposed when the context already has a node for its entry that has connected, so that
        /// node keeps its connection. A KV node there that never connected is disposed, and the seed takes its place.
        /// </remarks>
        /// <param name="seed">The connected seed, or null when it was dropped.</param>
        /// <param name="cancellationToken">Cancels the wait for the config lock.</param>
        internal async Task AddBootstrapNodesAsync(IClusterNode seed, CancellationToken cancellationToken)
        {
            var seedKept = false;
            try
            {
                await _configMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var view = ClusterNodesConfig;
                    var entries = view.GetClusterNodes();
                    foreach (var nodeAdapter in entries)
                    {
                        //log any alternate address mapping
                        _logger.LogInformation(nodeAdapter.ToString());
                    }

                    seedKept = seed is not null && TryAddSeed(seed, entries, GetEndPoints(entries));
                    ReconcileClusterNodes(view);
                }
                finally
                {
                    _configMutex.Release();
                }
            }
            finally
            {
                if (seed is not null && !seedKept)
                {
                    seed.Dispose();
                }
            }
        }

        private bool TryAddSeed(IClusterNode seed, List<NodeAdapter> entries, List<HostEndpointWithPort> endPoints)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (!ClusterNodeList.IsNodeOf(seed, endPoints[i], entries[i]))
                {
                    continue;
                }

                if (FindNodeAt(new List<IClusterNode>(Nodes), endPoints[i], entries[i]) is { } existing)
                {
                    // A node that never connected has no connection to keep, and the seed has one.
                    if (!existing.HasKv || !existing.IsAwaitingFirstConnection)
                    {
                        _logger.LogDebug("Bootstrapping: keeping cluster node [{node}] and closing the seed.",
                            _redactor.SystemData(endPoints[i]));
                        return false;
                    }

                    _logger.LogDebug("Bootstrapping: replacing cluster node [{node}], which never connected, with the seed.",
                        _redactor.SystemData(endPoints[i]));
                    RemoveNode(existing);
                }

                _logger.LogInformation("Bootstrapping: initializing global bootstrap node [{node}].",
                    _redactor.SystemData(endPoints[i]));
                seed.NodesAdapter = entries[i];
                AddNode(seed);
                return true;
            }

            _logger.LogInformation("Bootstrapping: removing seed node [{node}], as no map node has its address.",
                _redactor.SystemData(seed.EndPoint));
            return false;
        }

        public ValueTask<IBucket> GetOrCreateBucketAsync(string name)
        {
            if (Buckets.TryGetValue(name, out var bucket))
            {
                return new ValueTask<IBucket>(bucket);
            }

            // Splitting this method into two parts avoids the expense of building an async state machine
            // when the bucket is already created, which is the most common scenario.
            return new ValueTask<IBucket>(GetOrCreateBucketLockedAsync(name));
        }

        public async Task<IBucket> GetOrCreateBucketLockedAsync(string name)
        {
            Exception lastException = null;

            await _semaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                //Bucket was already created by the previously waiting thread
                if (Buckets.TryGetValue(name, out var bucket))
                {
                    return bucket;
                }

                // One map wait for the whole open, so the open lock is not held once per endpoint.
                var mapWait = new MapWaitBudget(_timeProvider, ClusterOptions.KvTimeout);
                foreach (var server in GetHostEndpointWithPorts(ClusterOptions.EnableTls))
                {
                    try
                    {
                        bucket = await CreateAndBootStrapBucketAsync(name, server, mapWait)
                            .ConfigureAwait(false);

                        if (_disposed != 0)
                        {
                            // Some awaits in CreateAndBootStrapBucketAsync do not observe cancellation, for
                            // example the node connect and BootstrapAsync. So a bucket can bootstrap after
                            // Dispose() emptied Buckets. DisposeAsync() removes it from Buckets and the config
                            // handler. The check also stops the next endpoint from running on a disposed context.
                            await bucket.DisposeAsync().ConfigureAwait(false);
                            throw new ObjectDisposedException(nameof(ClusterContext));
                        }

                        if (bucket is Bootstrapping.IBootstrappable {IsBootstrapped: true})
                        {
                            return bucket;
                        }
                    }
                    catch (RateLimitedException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        _logger.LogInformation(LoggingEvents.BootstrapEvent, e,
                            "Bootstrapping: cannot bootstrap bucket {name}.", name);
                        lastException = e;
                        if (_disposed != 0)
                        {
                            // The context is disposed - whatever just failed, retrying another
                            // endpoint against a disposed context wastes work and risks surfacing an
                            // unrelated ObjectDisposedException (e.g. from touching the already-disposed
                            // _tokenSource) in place of this exception, which is the meaningful one.
                            throw;
                        }
                        if (e is System.Security.Authentication.AuthenticationException authException
                            && authException.Message.Contains("certificate"))
                        {
                            if (_clusterOptions.EffectiveEnableTls
                                && !_clusterOptions.TlsSettings.KvIgnoreRemoteCertificateNameMismatch
                                && _clusterOptions.TlsSettings.KvCertificateValidationCallback == null
                                && !_clusterOptions.IsCapella)
                            {
                                throw new Exceptions.InvalidArgumentException("When TLS is enabled, the cluster environment's security config must specify" +
                                          $" the {nameof(ClusterOptions.TlsSettings.KvCertificateValidationCallback)}" +
                                          $" or use {nameof(ClusterOptions.TlsSettings.KvIgnoreRemoteCertificateNameMismatch)}" +
                                          " (Unless connecting to cloud.couchbase.com.)", authException);
                            }
                        }
                    }
                }
            }
            finally
            {
                try
                {
                    _semaphore.Release();
                }
                catch (ObjectDisposedException)
                {
                    // The context was disposed while this call was in flight. There's no one left
                    // to release the semaphore for, and rethrowing here would mask the bucket or
                    // exception the try block above already produced.
                }
            }

            if(lastException != null)
            {
                throw lastException;
            }
            throw new BucketNotFoundException(name);
        }

        /// <summary>
        /// The KV nodes of <see cref="ClusterNodesConfig"/>, the newest config of any kind. Without it, the KV nodes
        /// of <see cref="GlobalConfig"/>, and without that, the hosts of the connection string.
        /// </summary>
        private IEnumerable<HostEndpointWithPort> GetHostEndpointWithPorts(bool? enableTls)
        {
            var config = ClusterNodesConfig ?? GlobalConfig;
            if (config != null)
            {
                return config.GetBootstrapEndpoints(enableTls);
            }
            return ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(enableTls);
        }

        /// <param name="name">The name of the bucket.</param>
        /// <param name="endpoint">The node to bootstrap from.</param>
        /// <param name="mapWait">The time the open may still wait for a vBucket map. All endpoints of one open share
        /// it.</param>
        /// <remarks>
        /// The open creates a node of its own on the endpoint, which selects the bucket on each connection. A refused
        /// bucket or a failed connect fails this endpoint. A failed open disposes only what it created.
        /// </remarks>
        public async Task<BucketBase> CreateAndBootStrapBucketAsync(string name, HostEndpointWithPort endpoint,
            MapWaitBudget mapWait)
        {
            // Read once, because the property throws after the context is disposed.
            var token = CancellationToken;
            var node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint, null, name, token)
                .ConfigureAwait(false);

            BucketConfig config;
            BucketType type;
            try
            {
                _logger.LogDebug("Bootstrapping {bucket}", name);
                config = await FetchBucketConfigAsync(node, name, token).ConfigureAwait(false);

                //Determine the bucket type to create based off the bucket capabilities
                type = config.BucketCapabilities.Contains("cccp") ? BucketType.Couchbase : BucketType.Memcached;

                // A Memcached bucket has no vBucket map.
                if (type == BucketType.Couchbase && !config.HasVBucketMap())
                {
                    config = await WaitForVBucketMapAsync(node, name, config, mapWait, token)
                        .ConfigureAwait(false);
                }

                var nodeAdapter = config.GetNodes().Find(x=>HostEndpointWithPort.Create(x, ClusterOptions) == endpoint);
                node.NodesAdapter = nodeAdapter ??
                                    throw new NullReferenceException($"Could not find an adapter for {endpoint}.");
            }
            catch
            {
                node.Dispose();
                throw;
            }

            BucketBase bucket;
            try
            {
                var bucketFactory = ServiceProvider.GetRequiredService<IBucketFactory>();
                bucket = bucketFactory.Create(name, type, config);
            }
            catch
            {
                node.Dispose();
                throw;
            }

            var bootstrapped = false;
            try
            {
                node.Owner = bucket;
                bucket.Nodes.Add(node);

                _logger.LogInformation("Bootstrapping: created a {bucketType} bucket for {name}.",
                    type.GetDescription(), _redactor.MetaData(name));

                await bucket.BootstrapAsync(node).ConfigureAwait(false);
                if (bucket is Bootstrapping.IBootstrappable { IsBootstrapped: true })
                {
                    RegisterBucket(bucket);
                    UpdateAppTelemetryConfig(bucket.CurrentConfig);
                    bootstrapped = true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Bootstrapping: could not bootstrap bucket {name}.",
                    _redactor.MetaData(name));
                throw;
            }
            finally
            {
                // Dispose removes the bucket and its nodes, and stops its bootstrapper and config refresher.
                if (!bootstrapped)
                {
                    await bucket.DisposeAsync().ConfigureAwait(false);
                }
            }
            return bucket;
        }

        /// <summary>
        /// Fetches the config of a bucket from a node of the bucket over KV, or over HTTP when KV does not serve it, as
        /// for a Memcached bucket.
        /// </summary>
        private async Task<BucketConfig> FetchBucketConfigAsync(IClusterNode node, string name, CancellationToken token)
        {
            try
            {
                _logger.LogDebug("Bootstrapping: fetching the config using CCCP for bucket {name}.",
                    _redactor.MetaData(name));
                return await node.GetClusterMap(cancellationToken: token).ConfigureAwait(false);
            }
            catch (DocumentNotFoundException)
            {
                // KV does not serve the config, so the fetch goes on over HTTP.
            }

            _logger.LogInformation("Bootstrapping: switching to HTTP Streaming for bucket {name}.",
                _redactor.MetaData(name));
            return await _httpClusterMap.GetClusterMapAsync(name, node.EndPoint, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches the config of a new bucket again until it has a vBucket map, or until <paramref name="mapWait"/>
        /// has no time left. It then returns the last config, and a config with no map fails the bootstrap. A failed
        /// fetch is retried while time is left.
        /// </summary>
        /// <remarks>
        /// Before 7.6.2 the server can send the config of a new bucket before it has built the map (MB-60405).
        /// The caller holds the open lock, so other bucket opens wait too.
        /// The budget limits the delays between fetches. Each fetch keeps its own timeout, so the last fetch can
        /// end after the budget.
        /// </remarks>
        private async Task<BucketConfig> WaitForVBucketMapAsync(IClusterNode node, string name, BucketConfig config,
            MapWaitBudget mapWait, CancellationToken token)
        {
            var left = mapWait.Start();
            if (left > TimeSpan.Zero)
            {
                _logger.LogInformation(
                    "Bootstrapping: the config of bucket {name} has no vBucket map yet. Waiting for it.",
                    _redactor.MetaData(name));

                var interval = ClusterOptions.ConfigPollFloorInterval > TimeSpan.Zero
                    ? ClusterOptions.ConfigPollFloorInterval
                    : ClusterOptions.DefaultConfigPollFloorInterval;
                do
                {
                    await _timeProvider.Delay(interval < left ? interval : left, token).ConfigureAwait(false);
                    try
                    {
                        config = await FetchBucketConfigAsync(node, name, token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (!token.IsCancellationRequested && mapWait.Left > TimeSpan.Zero)
                    {
                        _logger.LogDebug(ex, "Bootstrapping: a config fetch for bucket {name} failed while " +
                            "waiting for a vBucket map. Trying again.", _redactor.MetaData(name));
                    }

                    left = mapWait.Left;
                }
                while (!config.HasVBucketMap() && left > TimeSpan.Zero);
            }

            if (!config.HasVBucketMap())
            {
                _logger.LogWarning("Bootstrapping: the config of bucket {name} has no vBucket map after the " +
                    "KvTimeout of {kvTimeout}. The bootstrap from this node fails.",
                    _redactor.MetaData(name), ClusterOptions.KvTimeout);
            }

            return config;
        }

        /// <summary>
        /// The time one bucket open may wait for a vBucket map, shared by every endpoint the open tries. It counts
        /// from the first config of the open that has no map, so a slow failing endpoint before it does not use it.
        /// </summary>
        internal sealed class MapWaitBudget(TimeProvider timeProvider, TimeSpan budget)
        {
            private long? _started;

            /// <summary>
            /// The time left. It is the full budget until <see cref="Start"/> runs.
            /// </summary>
            public TimeSpan Left
            {
                get
                {
                    if (_started is not { } started)
                    {
                        return budget;
                    }

                    var left = budget - timeProvider.GetElapsedTime(started);
                    return left > TimeSpan.Zero ? left : TimeSpan.Zero;
                }
            }

            /// <summary>
            /// Starts the budget on the first call, then returns the time left.
            /// </summary>
            public TimeSpan Start()
            {
                _started ??= timeProvider.GetTimestamp();
                return Left;
            }
        }

        /// <summary>
        /// The shortest time between the starts of two rebootstraps from the connection string.
        /// </summary>
        internal static readonly TimeSpan MinTimeBetweenRebootstraps = TimeSpan.FromSeconds(10);

        /// <summary>
        /// The last rebootstrap from the connection string. It is complete when none runs.
        /// </summary>
        internal Task LastRebootstrap { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// A config refresher calls this when every node failed to return a config. This then starts a rebootstrap in
        /// the background. It reads the DNS SRV records again when the connection string is a DNS SRV one, then
        /// bootstraps the cluster and every bucket on the hosts it finds. One rebootstrap runs at a time, at most one
        /// starts every <see cref="MinTimeBetweenRebootstraps"/>, and a call in between does nothing.
        /// </summary>
        /// <remarks>
        /// With a DNS SRV connection string the cluster may have moved, so every signal counts. With any other
        /// connection string only a client whose cluster KV nodes never connected rebootstraps, and only while no bucket
        /// is open, because the bootstrap is then the only way to get a new config.
        /// </remarks>
        internal void SignalConfigRefreshFailed()
        {
            if (ClusterOptions.ConnectionStringValue is not { IsDnsSrv: true }
                && (HasOpenBuckets || !IsAwaitingEveryClusterConnection()))
            {
                return;
            }

            lock (_rebootstrapLock)
            {
                if (!LastRebootstrap.IsCompleted
                    || (_lastRebootstrap is { } last
                        && _timeProvider.GetElapsedTime(last) < MinTimeBetweenRebootstraps))
                {
                    return;
                }

                _lastRebootstrap = _timeProvider.GetTimestamp();
                LastRebootstrap = Task.Run(RebootstrapFromConnectionStringAsync);
            }
        }

        /// <summary>
        /// True when the context has a live KV cluster node and none of them ever had a connection.
        /// </summary>
        internal bool IsAwaitingEveryClusterConnection()
        {
            var awaiting = false;
            foreach (var node in Nodes)
            {
                if (!node.HasKv || node.IsDead)
                {
                    continue;
                }

                if (!node.IsAwaitingFirstConnection)
                {
                    return false;
                }

                awaiting = true;
            }

            return awaiting;
        }

        private async Task RebootstrapFromConnectionStringAsync()
        {
            try
            {
                _logger.LogInformation("Bootstrapping: no node returned a config, so the SDK bootstraps again from the connection string.");
                await BootstrapGlobalAsync().ConfigureAwait(false);
                await RebootstrapAllBuckets().ConfigureAwait(false);
            }
            catch (Exception) when (_disposed != 0)
            {
                // Dispose cancels the bootstrap, which is not a failure.
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bootstrapping: the rebootstrap from the connection string failed.");
            }
        }

        public async Task RebootstrapAllBuckets()
        {
            foreach(var bucket in Buckets)
            {
                await RebootStrapAsync(bucket.Key).ConfigureAwait(false);
            }
        }
        public async Task RebootStrapAsync(string name)
        {
            if(Buckets.TryGetValue(name, out var bucket))
            {
                //start going through the bootstrap list trying to connect
                foreach (var endpoint in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions
                    .EnableTls))
                {
                    _logger.LogInformation($"Bootstrapping: a rebootstrap action is starting on host {endpoint}.");

                    // A node made only for the fetch stays out of the bucket list, so a concurrent apply cannot
                    // remove it. The apply of the fetched config creates the bucket node there if the map lists it.
                    IClusterNode fetchNode = null;
                    if (!bucket.Nodes.TryGet(endpoint, out var node))
                    {
                        node = fetchNode = await _clusterNodeFactory
                            .CreateAndConnectAsync(endpoint, null, name, CancellationToken)
                            .ConfigureAwait(false);
                    }

                    try
                    {
                        var config = await FetchBucketConfigAsync(node, name, CancellationToken)
                            .ConfigureAwait(false);

                        //make sure the bucket has the latest config as the current config
                        config.IgnoreRev = true;
                        await bucket.ConfigUpdatedAsync(config).ConfigureAwait(false);
                        // Pushes seen before can come from an older cluster behind the same DNS name.
                        (bucket as CouchbaseBucket)?.ConfigRefresher.ForgetPushedVersions();
                        UpdateAppTelemetryConfig(config);

                        return;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Could not bootstrap bucket {name}.", _redactor.MetaData(name));
                        UnRegisterBucket(bucket);
                    }
                    finally
                    {
                        fetchNode?.Dispose();
                    }
                }
            }
            else
            {
                throw new BucketNotFoundException(name);
            }
        }

        private readonly SemaphoreSlim _configMutex = new(1, 1);
        private readonly object _featureSupportLock = new();

        /// <summary>
        /// Gives a bucket one node of its own per KV entry of its config, and removes the bucket nodes that left the
        /// map. A config that is the view also brings the cluster nodes in line with it.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When every KV entry has its bucket node, no bucket node left the map and the cluster nodes need no change,
        /// only the adapters change and the config lock is not taken. The update then does not wait while another apply
        /// holds that lock to connect nodes.
        /// </para>
        /// <para>
        /// The cluster nodes follow <see cref="ClusterNodesConfig"/>, see <see cref="ReconcileClusterNodes"/>. A
        /// config that becomes the view runs that reconcile. A Memcached bucket config neither follows nor sets the view.
        /// </para>
        /// <para>
        /// A newer config becomes <see cref="ClusterNodesConfig"/> before it reads the nodes. A reconcile that adds a
        /// node at the same time then either sees the newer view after its add, or the newer config sees the node.
        /// </para>
        /// </remarks>
        public Task ProcessClusterMapAsync(BucketBase bucket, BucketConfig config)
        {
            _logger.LogDebug("ProcessClusterMap: {0} - {1}", bucket.Name, config.ConfigVersion);
            List<NodeAdapter> mapNodes;
            List<HostEndpointWithPort> endPoints;
            try
            {
                mapNodes = config.GetNodes();
                endPoints = GetEndPoints(mapNodes);

                if (FollowsClusterNodes(bucket, config))
                {
                    UpdateClusterNodesConfig(config);
                    if (ClusterNodesConfig is { } view && ReferenceEquals(view, config) && !TryMatchClusterNodes(view))
                    {
                        return ProcessClusterMapUnderLockAsync(bucket, config, mapNodes, endPoints);
                    }
                }

                if (TryMatchBucketNodes(bucket, mapNodes, endPoints))
                {
                    return Task.CompletedTask;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(LoggingEvents.ConfigEvent, "ERROR:{e}", e);
                return Task.FromException(e);
            }

            return ProcessClusterMapUnderLockAsync(bucket, config, mapNodes, endPoints);
        }

        private async Task ProcessClusterMapUnderLockAsync(BucketBase bucket, BucketConfig config,
            List<NodeAdapter> mapNodes, List<HostEndpointWithPort> endPoints)
        {
            await _configMutex.WaitAsync().ConfigureAwait(false);

            try
            {
                // A cluster config can apply while this call waits for the lock, so read the order again.
                if (FollowsClusterNodes(bucket, config))
                {
                    UpdateClusterNodesConfig(config);
                    if (ClusterNodesConfig is { } view && ReferenceEquals(view, config) && !IsReconciled(view))
                    {
                        ReconcileClusterNodes(view);
                    }
                }

                await ReconcileBucketNodesAsync(bucket, config, mapNodes, endPoints).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(LoggingEvents.ConfigEvent, "ERROR:{e}", e);
                throw;
            }
            finally
            {
                _configMutex.Release();
            }
        }

        /// <summary>
        /// Whether a bucket config moves <see cref="ClusterNodesConfig"/>. A Memcached bucket config never does, and an
        /// older config does not.
        /// </summary>
        private bool FollowsClusterNodes(BucketBase bucket, BucketConfig config) =>
            bucket.BucketType != BucketType.Memcached && !IsOlderThanClusterNodesConfig(config);

        /// <summary>
        /// The lock free form of <see cref="ReconcileBucketNodesAsync"/>. When every KV entry has its bucket node and
        /// no bucket node left the map, it gives each node the adapter of its entry and returns true. Otherwise it
        /// changes nothing and returns false.
        /// </summary>
        private static bool TryMatchBucketNodes(BucketBase bucket, List<NodeAdapter> mapNodes,
            List<HostEndpointWithPort> endPoints)
        {
            var nodes = bucket.Nodes.Snapshot();
            foreach (var node in nodes)
            {
                if (FindKvEntry(node, mapNodes, endPoints) < 0)
                {
                    return false;
                }
            }

            for (var i = 0; i < mapNodes.Count; i++)
            {
                if (mapNodes[i].IsKvNode && FindNode(nodes, endPoints[i], mapNodes[i]) is null)
                {
                    return false;
                }
            }

            foreach (var node in nodes)
            {
                node.NodesAdapter = mapNodes[FindKvEntry(node, mapNodes, endPoints)];
            }

            return true;
        }

        /// <summary>
        /// Gives a bucket one node of its own per KV entry of its config, under the config lock.
        /// </summary>
        /// <remarks>
        /// The new nodes connect in parallel, and every connect is awaited before the bucket changes. A node that
        /// refuses the bucket joins with no connection, and its pool keeps trying. Any other failed connect disposes
        /// the new nodes and fails the apply with no change to the bucket. Then the new nodes join, the kept nodes get
        /// their adapters, and the nodes that left the map are removed and disposed.
        /// </remarks>
        private async Task ReconcileBucketNodesAsync(BucketBase bucket, BucketConfig config,
            List<NodeAdapter> mapNodes, List<HostEndpointWithPort> endPoints)
        {
            var nodes = bucket.Nodes.Snapshot();
            List<Task<IClusterNode>> connects = null;
            for (var i = 0; i < mapNodes.Count; i++)
            {
                var nodeAdapter = mapNodes[i];

                //log any alternate address mapping
                _logger.LogInformation(nodeAdapter.ToString());

                if (!nodeAdapter.IsKvNode || FindNode(nodes, endPoints[i], nodeAdapter) is not null)
                {
                    continue;
                }

                _logger.LogDebug("Creating node {endPoint} for bucket {bucketName} using rev#{revision}",
                    _redactor.SystemData(endPoints[i]), _redactor.MetaData(bucket.Name), config.Rev);
                (connects ??= []).Add(ConnectBucketNode(bucket.Name, nodeAdapter, endPoints[i]));
            }

            var created = connects is null ? [] : await ConnectAllAsync(connects).ConfigureAwait(false);
            if (bucket.Disposed || _disposed != 0)
            {
                foreach (var node in created)
                {
                    node.Dispose();
                }

                return;
            }

            List<IClusterNode> kept = new(nodes.Count + created.Length);
            List<IClusterNode> left = null;
            foreach (var node in nodes)
            {
                var entry = FindKvEntry(node, mapNodes, endPoints);
                if (entry < 0)
                {
                    (left ??= []).Add(node);
                    continue;
                }

                node.NodesAdapter = mapNodes[entry];
                kept.Add(node);
            }

            if (created.Length == 0 && left is null)
            {
                return;
            }

            foreach (var node in created)
            {
                node.Owner = bucket;
                kept.Add(node);
            }

            // Adds the new nodes before it removes the others, so an endpoint can move from a node that left.
            bucket.Nodes.ReplaceAll(kept);

            if (left is not null)
            {
                foreach (var node in left)
                {
                    _logger.LogDebug("Removing node {endPoint} from bucket {bucketName}, as rev#{revision} does not list it.",
                        _redactor.SystemData(node.EndPoint), _redactor.MetaData(bucket.Name), config.Rev);
                    node.Dispose();
                }
            }

            // A close that ran during the replace can miss the new nodes.
            if (bucket.Disposed)
            {
                foreach (var node in bucket.Nodes.Clear())
                {
                    node.Dispose();
                }
            }

            RecomputeClusterFeatureSupport();
        }

        private Task<IClusterNode> ConnectBucketNode(string bucketName, NodeAdapter nodeAdapter,
            HostEndpointWithPort endPoint)
        {
            try
            {
                // We want the BootstrapEndpoint to use the host name, not just the IP
                return _clusterNodeFactory.CreateAndConnectAsync(
                    new HostEndpointWithPort(nodeAdapter.Hostname, endPoint.Port), nodeAdapter, bucketName,
                    acceptRefusedBucket: true, CancellationToken);
            }
            catch (Exception e)
            {
                return Task.FromException<IClusterNode>(e);
            }
        }

        /// <summary>
        /// Awaits every connect. When one fails, it disposes the nodes of the others and throws the first error.
        /// </summary>
        private static async Task<IClusterNode[]> ConnectAllAsync(List<Task<IClusterNode>> connects)
        {
            try
            {
                return await Task.WhenAll(connects).ConfigureAwait(false);
            }
            catch
            {
                foreach (var connect in connects)
                {
                    if (connect.Status == TaskStatus.RanToCompletion)
                    {
                        connect.Result.Dispose();
                    }
                }

                throw;
            }
        }

        private static IClusterNode FindNode(List<IClusterNode> nodes, HostEndpointWithPort endPoint,
            NodeAdapter nodeAdapter)
        {
            foreach (var node in nodes)
            {
                if (ClusterNodeList.IsNodeOf(node, endPoint, nodeAdapter))
                {
                    return node;
                }
            }

            return null;
        }

        /// <summary>
        /// The index of the KV entry of the node, or -1 when no KV entry lists it.
        /// </summary>
        private static int FindKvEntry(IClusterNode node, List<NodeAdapter> mapNodes,
            List<HostEndpointWithPort> endPoints)
        {
            for (var i = 0; i < mapNodes.Count; i++)
            {
                if (mapNodes[i].IsKvNode && ClusterNodeList.IsNodeOf(node, endPoints[i], mapNodes[i]))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Brings the cluster nodes in line with a config, under the config lock. Each entry of
        /// <see cref="BucketConfigExtensions.GetClusterNodes"/> gets a cluster node, also a host where a bucket has a
        /// node. Each cluster node that the config does not list is removed and disposed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A new node joins at once and connects in the background, so the reconcile waits for no connect.
        /// </para>
        /// <para>
        /// The lock free bucket path can move <see cref="ClusterNodesConfig"/> and mark it reconciled while this runs on
        /// another thread. The view is read again after each AddNode, and a newer view restarts the reconcile against it,
        /// so a node that the newer view drops does not stay. A config that moves the view later reads the nodes after
        /// that AddNode, so it sees the new node.
        /// </para>
        /// </remarks>
        private void ReconcileClusterNodes(BucketConfig config)
        {
            var changed = false;
            while (true)
            {
                var entries = config.GetClusterNodes();
                var endPoints = GetEndPoints(entries);

                foreach (var node in Nodes)
                {
                    // A node with no adapter is the seed of a server before 6.5, which has no cluster config.
                    if (node.NodesAdapter != null && !IsInMap(node, entries, endPoints))
                    {
                        _logger.LogDebug("Removing node {endPoint}, as config revision {rev} does not list it.",
                            _redactor.SystemData(node.EndPoint), config.ConfigVersion);
                        RemoveNode(node);
                        changed = true;
                    }
                }

                var nodes = new List<IClusterNode>(Nodes);
                BucketConfig newerView = null;
                for (var i = 0; i < entries.Count; i++)
                {
                    if (HasNodeAt(nodes, endPoints[i], entries[i]))
                    {
                        SetAdapter(nodes, endPoints[i], entries[i]);
                        continue;
                    }

                    _logger.LogDebug("Creating cluster node {endPoint} for config revision {rev}.",
                        _redactor.SystemData(endPoints[i]), config.ConfigVersion);
                    var newNode = _clusterNodeFactory.CreateClusterNode(endPoints[i], entries[i]);
                    AddNode(newNode);
                    nodes.Add(newNode);
                    changed = true;

                    if (ClusterNodesConfig is { } view && !ReferenceEquals(view, config)
                        && view.ConfigVersion > config.ConfigVersion)
                    {
                        newerView = view;
                        break;
                    }
                }

                if (newerView is null)
                {
                    Volatile.Write(ref _reconciledClusterNodesConfig, config);

                    if (changed)
                    {
                        GlobalConfigRefresher.NodesChanged();
                    }

                    return;
                }

                _logger.LogDebug("Reconciling the nodes again for revision {rev}, as it replaced revision {old}.",
                    newerView.ConfigVersion, config.ConfigVersion);
                config = newerView;
            }
        }

        /// <summary>
        /// The lock free form of <see cref="ReconcileClusterNodes"/>. When the view needs no node created or
        /// removed, it gives the nodes their view adapter, marks the view as reconciled and returns true.
        /// Otherwise it changes nothing and returns false.
        /// </summary>
        private bool TryMatchClusterNodes(BucketConfig view)
        {
            if (IsReconciled(view))
            {
                return true;
            }

            var entries = view.GetClusterNodes();
            var endPoints = GetEndPoints(entries);
            // ClusterNodeList.TryGet reads the set with no lock, so match against one snapshot.
            var nodes = new List<IClusterNode>(Nodes);

            foreach (var node in nodes)
            {
                if (node.NodesAdapter != null && !IsInMap(node, entries, endPoints))
                {
                    return false;
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                if (!HasNodeAt(nodes, endPoints[i], entries[i]))
                {
                    return false;
                }
            }

            for (var i = 0; i < entries.Count; i++)
            {
                SetAdapter(nodes, endPoints[i], entries[i]);
            }

            Volatile.Write(ref _reconciledClusterNodesConfig, view);
            return true;
        }

        private static bool HasNodeAt(List<IClusterNode> nodes, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter) =>
            FindNodeAt(nodes, endPoint, nodeAdapter) is not null;

        private static IClusterNode FindNodeAt(List<IClusterNode> nodes, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter)
        {
            foreach (var node in nodes)
            {
                if (ClusterNodeList.IsNodeOf(node, endPoint, nodeAdapter))
                {
                    return node;
                }
            }

            return null;
        }

        private static void SetAdapter(List<IClusterNode> nodes, HostEndpointWithPort endPoint, NodeAdapter nodeAdapter)
        {
            foreach (var node in nodes)
            {
                if (ClusterNodeList.IsNodeOf(node, endPoint, nodeAdapter))
                {
                    node.NodesAdapter = nodeAdapter;
                }
            }
        }

        private List<HostEndpointWithPort> GetEndPoints(List<NodeAdapter> mapNodes)
        {
            var endPoints = new List<HostEndpointWithPort>(mapNodes.Count);
            foreach (var nodeAdapter in mapNodes)
            {
                endPoints.Add(HostEndpointWithPort.Create(nodeAdapter, _clusterOptions));
            }

            return endPoints;
        }

        private static bool IsInMap(IClusterNode node, List<NodeAdapter> mapNodes, List<HostEndpointWithPort> endPoints)
        {
            for (var i = 0; i < mapNodes.Count; i++)
            {
                if (ClusterNodeList.IsNodeOf(node, endPoints[i], mapNodes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task ConfigUpdatedAsync(BucketConfig globalConfig)
        {
#if DEBUG
            _logger.LogDebug("Global nodes: {nodes}", this.Nodes.Count);
            foreach (var bucket in Buckets)
            {
                _logger.LogDebug("Bucket {name} nodes: {nodes}", bucket.Key, bucket.Value.ClusterNodes.Count());
            }
#endif

            // Every config reaches here, bucket configs included.
            UpdateClusterCapsConfig(globalConfig);

            // Merge ClusterLabels: prefer incoming values (latest from server), carry forward current values as fallback.
            // Both configs are updated so labels survive even if GlobalConfig is replaced.
            if (GlobalConfig is not null)
            {
                globalConfig.MergeClusterLabels(GlobalConfig);
            }

            if (globalConfig.Name == Name)
            {
                using var cts = new CancellationTokenSource();
                await _configMutex.WaitAsync(cts.Token).ConfigureAwait(false);

                try
                {
                    _logger.LogDebug("Got global config revision: {rev}", globalConfig.Rev);

                    if (!globalConfig.HasConfigChanges(GlobalConfig, Name))
                    {
                        return;
                    }

                    // A newer bucket config can have removed nodes that this config still lists. So an older config
                    // changes no node, but it is still the newest cluster config and the poll sends its version.
                    // A config at the revision of a reconciled view has no node work left.
                    var view = ClusterNodesConfig;
                    var target = globalConfig;
                    if (!globalConfig.IgnoreRev && view is not null && globalConfig.ConfigVersion <= view.ConfigVersion)
                    {
                        if (globalConfig.ConfigVersion < view.ConfigVersion || IsReconciled(view))
                        {
                            if (_logger.IsEnabled(LogLevel.Debug))
                            {
                                _logger.LogDebug("Storing global config revision {rev} with no node change, as config revision {view} set the nodes.",
                                    globalConfig.ConfigVersion, view.ConfigVersion);
                            }

                            GlobalConfig = globalConfig;
                            return;
                        }

                        // The view at this revision has not finished a reconcile, as its apply waits for the lock.
                        target = view;
                    }

                    ReconcileClusterNodes(target);

                    GlobalConfig = globalConfig;
                    UpdateClusterNodesConfig(globalConfig);
                }
                finally
                {
                    _configMutex.Release();
                }
            }
        }

        public string Name => BucketConfig.GlobalBucketName;
        public IEnumerable<IClusterNode> ClusterNodes => Nodes;

        public void Dispose()
        {
            // Interlocked, not a check-then-set on a bool: concurrent callers could otherwise all
            // observe "not disposed" before any of them records it, and each would run the full body.
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            // Cancel first so anything awaiting or holding _semaphore (e.g. an in-flight
            // GetOrCreateBucketLockedAsync call) unwinds promptly instead of racing the
            // semaphore's disposal below.
            try
            {
                _tokenSource?.Cancel();
            }
            catch (Exception ex)
            {
                // A throwing cancellation callback must not abandon the rest of teardown: _disposed
                // is already latched above, so anything skipped here would be skipped for good on a
                // retried Dispose().
                _logger.LogWarning(ex, "Error cancelling the cluster token source during disposal.");
            }

            GlobalConfigRefresher.Dispose();
            _configHandler?.Dispose();
            _semaphore.Dispose();
            _tokenSource?.Dispose();

            foreach (var ownedObject in _ownedObjects)
            {
                ownedObject.Dispose();
            }
            _ownedObjects.Clear();

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


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
