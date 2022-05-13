using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Core
{
    internal class ClusterContext : IDisposable
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
        private readonly IRedactor _redactor;
        private readonly IConfigHandler _configHandler;
        private readonly IClusterNodeFactory _clusterNodeFactory;
        private readonly CancellationTokenSource _tokenSource;
        protected readonly ConcurrentDictionary<string, BucketBase> Buckets = new();
        private bool _disposed;
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
            _redactor = ServiceProvider.GetRequiredService<IRedactor>();
            _configHandler = ServiceProvider.GetRequiredService<IConfigHandler>();
            _clusterNodeFactory = ServiceProvider.GetRequiredService<IClusterNodeFactory>();
            _httpClusterMapFactory = ServiceProvider.GetRequiredService<IHttpClusterMapFactory>();
            _httpClusterMap = _httpClusterMapFactory.Create(this);
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

        public void RemoveBucket(BucketBase bucket)
        {
            if (Buckets.TryRemove(bucket.Name, out _))
            {
                _configHandler.Unsubscribe(bucket);
            }
        }

        public void PublishConfig(BucketConfig bucketConfig)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(LoggingEvents.ConfigEvent,
                    JsonSerializer.Serialize(bucketConfig, InternalSerializationContext.Default.BucketConfig));
            }

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
                case ServiceType.Eventing:
                    node = Nodes.GetRandom(x => x.HasEventing);
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
                    _logger.LogDebug("Using node owned by {bucket} using revision {endpoint}",
                        _redactor.UserData(node1.Owner?.Name), _redactor.SystemData(node1.EndPoint));
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
            var node = Nodes.GetRandom();
            if(node == null)
            {
                ThrowHelper.ThrowServiceNotAvailableException(ServiceType.Management);
            }
            return node;
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

        public IClusterNode GetUnassignedNode(HostEndpointWithPort endpoint)
        {
            return Nodes.FirstOrDefault(
                x => !x.IsAssigned && x.EndPoint.Equals(endpoint));
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

            //defer throwing exceptions until all nodes have been tried.
            //if this is non null that means and we haven't bootstrapped
            //then bootstrapping has failed and we can throw the agg exception
            List<Exception> exceptions = new List<Exception>();

            //Try to bootstrap each node in the servers list - either from DNS-SRV lookup or from client configuration
            foreach (var server in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions.EnableTls))
            {
                IClusterNode node = null;
                try
                {
                    _logger.LogDebug("Bootstrapping: global bootstrapping with node {server}", server);
                    node = await _clusterNodeFactory
                        .CreateAndConnectAsync(server, CancellationToken)
                        .ConfigureAwait(false);

                    GlobalConfig = await node.GetClusterMap().ConfigureAwait(false);
                    GlobalConfig.SetEffectiveNetworkResolution(ClusterOptions);

                    //ignore the exceptions since at least one node bootstrapped
                    exceptions.Clear();
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

                    // something else failed, try the next hostname
                    _logger.LogDebug(e, "Bootstrapping: attempted global bootstrapping on endpoint {endpoint} has failed.",
                        server);

                    //hold on to the exception to create agg exception if none complete.
                    exceptions.Add(e);

                    //skip to next endpoint and try again
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

                        var hostEndpoint = HostEndpointWithPort.Create(nodeAdapter, ClusterOptions);
                        if (server.Equals(hostEndpoint)) //this is the bootstrap node so update
                        {
                            _logger.LogInformation("Bootstrapping: initializing global bootstrap node [{node}].",
                                _redactor.SystemData(hostEndpoint.ToString()));

                            node.NodesAdapter = nodeAdapter;
                            SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                            AddNode(node);
                        }
                        else
                        {
                            _logger.LogInformation("Bootstrapping: initializing a global non-bootstrap node [{node}]",
                                _redactor.SystemData(hostEndpoint.ToString()));

                            var newNode = await _clusterNodeFactory
                                .CreateAndConnectAsync(hostEndpoint, nodeAdapter,
                                    CancellationToken).ConfigureAwait(false);
                            SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                            AddNode(newNode);
                        }
                    }
                    return;
                }
                catch (Exception e)
                {
                    _logger.LogDebug(e, "Bootstrapping: attempted global bootstrapping on endpoint {endpoint} has failed.",
                        _redactor.MetaData(server));
                }
            }

            if (exceptions?.Count > 0)
            {
                //for backwards compatibility return an auth exception if one exists (logs will show others).
                var authException = exceptions.FirstOrDefault(e => e.GetType() == typeof(AuthenticationFailureException));
                if(authException != null)
                {
                    ExceptionDispatchInfo.Capture(authException).Throw();
                }
                //Not an auth exception but still cannot bootstrap so return all the exceptions
                throw new AggregateException("Bootstrapping has failed!", exceptions);
            }

        }

        public async ValueTask<IBucket> GetOrCreateBucketAsync(string name)
        {
            if (Buckets.TryGetValue(name, out var bucket))
            {
                return bucket;
            }

            Exception lastException = null;
            await _semaphore.WaitAsync(CancellationToken).ConfigureAwait(false);
            try
            {
                //Bucket was already created by the previously waiting thread
                if (Buckets.TryGetValue(name, out bucket))
                {
                    return bucket;
                }

                foreach (var server in ClusterOptions.ConnectionStringValue.GetBootstrapEndpoints(ClusterOptions.EnableTls))
                {
                    try
                    {
                        bucket = await CreateAndBootStrapBucketAsync(name, server)
                            .ConfigureAwait(false);

                        if ((bucket is Bootstrapping.IBootstrappable bootstrappable) && bootstrappable.IsBootstrapped)
                            return bucket;
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
                        if (e is System.Security.Authentication.AuthenticationException authException
                            && authException.Message.Contains("certificate"))
                        {
                            if (_clusterOptions.EffectiveEnableTls
                                && !_clusterOptions.KvIgnoreRemoteCertificateNameMismatch
                                && _clusterOptions.KvCertificateCallbackValidation == null
                                && !_clusterOptions.IsCapella)
                            {
                                throw new Exceptions.InvalidArgumentException("When TLS is enabled, the cluster environment's security config must specify" +
                                          $" the {nameof(ClusterOptions.KvCertificateCallbackValidation)}" +
                                          $" or use {nameof(ClusterOptions.KvIgnoreRemoteCertificateNameMismatch)}" +
                                          " (Unless connecting to cloud.couchbase.com.)", authException);
                            }
                        }
                    }
                }
            }
            finally
            {
                _semaphore.Release();
            }

            if(lastException != null)
            {
                throw lastException;
            }
            throw new BucketNotFoundException(name);
        }

        public async Task<BucketBase> CreateAndBootStrapBucketAsync(string name, HostEndpointWithPort endpoint)
        {
            BucketBase bucket;
            var node = GetUnassignedNode(endpoint);
            if (node == null)
            {
                node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint, CancellationToken).ConfigureAwait(false);
                AddNode(node);
            }

            BucketConfig config;
            try
            {
                //The bucket must be selected so that a bucket specific config is returned
                await node.SelectBucketAsync(name).ConfigureAwait(false);

                _logger.LogDebug("Bootstrapping: fetching the config using CCCP for bucket {name}.",
                    _redactor.MetaData(name));

               //First try CCCP to fetch the config
               config = await node.GetClusterMap().ConfigureAwait(false);
            }
            catch (DocumentNotFoundException)
            {
                _logger.LogInformation("Bootstrapping: switching to HTTP Streaming for bucket {name}.",
                    _redactor.MetaData(name));

                //In this case CCCP has failed for whatever reason
                //We need to now try HTTP Streaming for config fetching
                config = await _httpClusterMap.GetClusterMapAsync(
                    name, node.EndPoint, CancellationToken.None).ConfigureAwait(false);
            }

            //Determine the bucket type to create based off the bucket capabilities
            var type = config.BucketCapabilities.Contains("cccp") ? BucketType.Couchbase : BucketType.Memcached;
            var bucketFactory = ServiceProvider.GetRequiredService<IBucketFactory>();
            bucket = bucketFactory.Create(name, type, config);
            node.Owner = bucket;

            _logger.LogInformation("Bootstrapping: created a {bucketType} bucket for {name}.",
                type.GetDescription(), _redactor.MetaData(name));

            try
            {
                await bucket.BootstrapAsync(node).ConfigureAwait(false);
                if ((bucket is Bootstrapping.IBootstrappable bootstrappable) && bootstrappable.IsBootstrapped)
                {
                    RegisterBucket(bucket);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Bootstrapping: could not bootstrap bucket {name}.",
                    _redactor.MetaData(name));

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
                    var node = GetUnassignedNode(endpoint);
                    if (node == null)
                    {
                        node = await _clusterNodeFactory.CreateAndConnectAsync(endpoint, CancellationToken)
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
            foreach (var nodeAdapter in config.GetNodes())
            {
                //log any alternate address mapping
                _logger.LogInformation(nodeAdapter.ToString());

                var endPoint = HostEndpointWithPort.Create(nodeAdapter, _clusterOptions);
                if (Nodes.TryGet(endPoint, out var bootstrapNode))
                {
                    if (bootstrapNode.Owner == null && bucket.BucketType != BucketType.Memcached)
                    {
                        _logger.LogDebug(
                            "Using existing node {endPoint} for bucket {bucket.Name} using rev#{config.Rev}",
                            _redactor.SystemData(endPoint), _redactor.MetaData(bucket.Name), config.Rev);

                        bootstrapNode.Owner = bucket;
                        if (bootstrapNode.HasKv)
                        {
                            await bootstrapNode.SelectBucketAsync(bucket.Name, CancellationToken).ConfigureAwait(false);
                            await bootstrapNode.HelloHello().ConfigureAwait(false);
                            SupportsPreserveTtl = bootstrapNode.ServerFeatures.PreserveTtl;
                        }
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

                var node = await _clusterNodeFactory.CreateAndConnectAsync(
                    // We want the BootstrapEndpoint to use the host name, not just the IP
                    new HostEndpointWithPort(nodeAdapter.Hostname, endPoint.Port),
                    nodeAdapter,
                    CancellationToken).ConfigureAwait(false);

                node.Owner = bucket;
                if (node.HasKv)
                {
                    await node.SelectBucketAsync(bucket.Name, CancellationToken).ConfigureAwait(false);
                    await node.HelloHello().ConfigureAwait(false);
                    SupportsPreserveTtl = node.ServerFeatures.PreserveTtl;
                }

                AddNode(node);
            }

            PruneNodes(config);
        }

        public void PruneNodes(BucketConfig config)
        {
            var existingEndpoints = config.GetNodes()
                .Select(p => HostEndpointWithPort.Create(p, _clusterOptions))
                .ToList();

            _logger.LogDebug("ExistingEndpoints: {endpoints}, revision {revision}.", existingEndpoints, config.Rev);

            var removedEndpoints = Nodes.Where(x =>
                !existingEndpoints.Any(y => x.KeyEndPoints.Any(z => z.Equals(y))));

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
