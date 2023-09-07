using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Collections;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using JsonSerializer = System.Text.Json.JsonSerializer;

#nullable enable

namespace Couchbase
{
    internal class CouchbaseBucket : BucketBase
    {
        private readonly IVBucketKeyMapperFactory _vBucketKeyMapperFactory;
        private readonly LazyService<IViewClient> _viewClientLazy;
        private readonly LazyService<IViewIndexManagerFactory> _viewManagerLazy;
        private readonly LazyService<ICollectionManagerFactory> _collectionManagerLazy;
        private readonly SemaphoreSlim _configMutex = new(1, 1);

        // It isn't imperative that race conditions accessing these fields the first time must
        // always return the same singleton. In the unlikely event two threads access them the
        // first time simultaneously one may receive a temporary extra instance but that's okay.
        private IViewIndexManager? _viewIndexManager;
        private ICouchbaseCollectionManager? _collectionManager;

        internal CouchbaseBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory, ILogger<CouchbaseBucket> logger, TypedRedactor redactor, IBootstrapperFactory bootstrapperFactory,
            IRequestTracer tracer, IOperationConfigurator operationConfigurator, IRetryStrategy retryStrategy, BucketConfig config)
            : base(name, context, scopeFactory, retryOrchestrator, logger, redactor, bootstrapperFactory, tracer, operationConfigurator, retryStrategy, config)
        {
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));

            _viewClientLazy = new LazyService<IViewClient>(context.ServiceProvider);
            _viewManagerLazy = new LazyService<IViewIndexManagerFactory>(context.ServiceProvider);
            _collectionManagerLazy = new LazyService<ICollectionManagerFactory>(context.ServiceProvider);
        }

        public override IViewIndexManager ViewIndexes =>
            _viewIndexManager ??= _viewManagerLazy.GetValueOrThrow().Create(Name);

        /// <summary>
        /// The Collection Management API.
        /// </summary>
        /// <remarks>Volatile</remarks>
        public override ICouchbaseCollectionManager Collections =>
            _collectionManager ??= _collectionManagerLazy.GetValueOrThrow().Create(Name, this.CurrentConfig);

        public override async Task ConfigUpdatedAsync(BucketConfig newConfig)
        {
            using var cts = new CancellationTokenSource(Context.ClusterOptions.ConfigUpdatingTimeout);
            await _configMutex.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                if (newConfig.HasConfigChanges(CurrentConfig, Name))
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                    {
                        Logger.LogDebug(
                            "Processing cluster map for rev#{revision} on {bucketName} - old rev#{oldRevision}",
                            newConfig.Rev, Name, CurrentConfig?.Rev);
                        Logger.LogDebug(JsonSerializer.Serialize(newConfig,
                            InternalSerializationContext.Default.BucketConfig!));
                    }

                    if (newConfig.HasVBucketMapChanged(CurrentConfig) || newConfig.IgnoreRev)
                    {
                        Logger.LogDebug(LoggingEvents.ConfigEvent,
                            "Updating VB key mapper for rev#{revision} on {bucketName}", newConfig.Rev, Name);
                        KeyMapper = _vBucketKeyMapperFactory.Create(newConfig);
                    }

                    if (newConfig.HasClusterNodesChanged(CurrentConfig) || newConfig.IgnoreRev)
                    {
                        Logger.LogDebug(LoggingEvents.ConfigEvent,
                            "Updating cluster nodes for rev#{revision} on {bucketName}", newConfig.Rev, Name);
                        await Context.ProcessClusterMapAsync(this, newConfig).ConfigureAwait(false);
                        var nodes = Context.GetNodes(Name);

                        //update the local nodes collection
                        Nodes.Clear();
                        foreach (var clusterNode in nodes)
                        {
                            Nodes.Add(clusterNode);
                        }
                    }

                    //only accept the latest version if the processing was successful
                    CurrentConfig = newConfig;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(LoggingEvents.ConfigEvent, "Error processing cluster map rev#{config}:{ex}",
                    newConfig.Rev, ex);
            }
            finally
            {
                _configMutex.Release();
            }
            Logger.LogDebug(LoggingEvents.ConfigEvent,"Current revision for {bucketName} is rev#{revision}", Name, CurrentConfig?.Rev);
        }

        public override async Task ForceConfigUpdateAsync()
        {
            var configNode = Nodes.GetRandom(x => x.HasKv);
            var config = await configNode.GetClusterMap(CurrentConfig?.ConfigVersion).ConfigureAwait(false);
            Context.PublishConfig(config);
        }

        //TODO move Uri storage to ClusterNode - IBucket owns BucketConfig though
        private Uri GetViewUri()
        {
            var clusterNode = Context.GetRandomNodeForService(ServiceType.Views, Name);
            if (clusterNode?.ViewsUri == null)
            {
                throw new ServiceMissingException("Views Service cannot be located.");
            }
            return clusterNode.ViewsUri;
        }

        /// <inheritdoc />
        public override async Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = null)
        {
            ThrowIfBootStrapFailed();

            options ??= new ViewOptions();
            // create old style query
            var query = new ViewQuery(GetViewUri().ToString())
            {
                UseSsl = Context.ClusterOptions.EffectiveEnableTls
            };

            //Normalize to new naming convention for public API RFC#51
            var staleState = StaleState.None;
            if (options.ScanConsistencyValue == ViewScanConsistency.RequestPlus)
            {
                staleState = StaleState.False;
            }
            if (options.ScanConsistencyValue == ViewScanConsistency.UpdateAfter)
            {
                staleState = StaleState.UpdateAfter;
            }
            if (options.ScanConsistencyValue == ViewScanConsistency.NotBounded)
            {
                staleState = StaleState.Ok;
            }

            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(staleState);
            query.Limit(options.LimitValue);
            query.Skip(options.SkipValue);
            query.StartKey(options.StartKeyValue);
            query.StartKeyDocId(options.StartKeyDocIdValue);
            query.EndKey(options.EndKeyValue);
            query.EndKeyDocId(options.EndKeyDocIdValue);
            query.InclusiveEnd(options.InclusiveEndValue);
            query.Group(options.GroupValue);
            query.GroupLevel(options.GroupLevelValue);
            query.Key(options.KeyValue);
            query.Keys(options.KeysValue);
            query.Reduce(options.ReduceValue);
            query.Development(options.DevelopmentValue);
            query.Debug(options.DebugValue);
            query.Namespace(options.NamespaceValue);
            query.OnError(options.OnErrorValue == ViewErrorMode.Stop);
            query.Timeout = options.TimeoutValue ?? Context.ClusterOptions.ViewTimeout;
            query.Serializer = options.SerializerValue;
            query.RetryStrategy = options.RetryStrategyValue ?? RetryStrategy;
            query.RequestSpan(options.RequestSpanValue);

            if (options.ViewOrderingValue == ViewOrdering.Decesending)
            {
                query.Desc();
            }
            else
            {
                query.Asc();
            }

            if (options.FullSetValue.HasValue && options.FullSetValue.Value)
            {
                query.FullSet();
            }

            foreach (var kvp in options.RawParameters)
            {
                query.Raw(kvp.Key, kvp.Value);
            }

            async Task<IViewResult<TKey, TValue>> Func()
            {
                var client1 = _viewClientLazy.GetValueOrThrow();
                return await client1.ExecuteAsync<TKey, TValue>(query).ConfigureAwait(false);
            }

            return await RetryOrchestrator.RetryAsync(Func, query).ConfigureAwait(false);
        }

        internal override async Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair tokenPair = default)
        {
            if (KeyMapper == null) ThrowHelper.ThrowInvalidOperationException($"Bucket {Name} is not bootstrapped.");

            if (op.RequiresVBucketId)
            {
                var vBucket = (VBucket) KeyMapper.MapKey(op.Key, op.WasNmvb());

                var endPoint = op.ReplicaIdx != null && op.ReplicaIdx > -1
                    ? vBucket.LocateReplica(op.ReplicaIdx.GetValueOrDefault())
                    : vBucket.LocatePrimary();

                op.VBucketId = vBucket.Index;

                if (Nodes.TryGet(endPoint.GetValueOrDefault(), out var clusterNode))
                {
                    Logger.LogDebug("Sending op {operation} with {key} to {node} using VBID: {vbucketid}", op.OpCode, op.Key, clusterNode.EndPoint, op.VBucketId);
                    return await clusterNode.SendAsync(op, tokenPair).ConfigureAwait(false);
                }

                //We could not find a candidate node to send so put into the retry queue
                //its likely were between cluster map updates and we'll try again later
                throw new NodeNotAvailableException(
                    $"Cannot find a Couchbase Server node for {endPoint}.");
            }

            //Send CID request only to nodes in the VBucketMap
            if (op is GetCid getCid)
            {
                var key = getCid.CoerceKey;
                var vBucket = (VBucket)KeyMapper.MapKey(key);
                var endPoint = vBucket.LocatePrimary();

                if (Nodes.TryGet(endPoint.GetValueOrDefault(), out var clusterNode))
                {
                    return await clusterNode.SendAsync(op, tokenPair).ConfigureAwait(false);
                }

                //We could not find a candidate node to send so put into the retry queue
                //its likely were between cluster map updates and we'll try again later
                throw new NodeNotAvailableException(
                    $"Cannot find a Couchbase Server node for {endPoint}.");
            }

            //Make sure we use a node with the data service
            var node = Nodes.GetRandom(x => x.HasKv);
            if (node == null)
                throw new NodeNotAvailableException(
                    $"Cannot find a Couchbase Server node for executing {op.GetType()}.");

            await node.SelectBucketAsync(Name, tokenPair).ConfigureAwait(false);
            return await node.SendAsync(op, tokenPair).ConfigureAwait(false);
        }

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            try
            {
                Logger.LogInformation("Bootstrapping: server negotiation started for {name}.", Redactor.UserData(Name));
                if (Context.ClusterOptions.HasNetworkResolution)
                {
                    //Network resolution determined at the GCCCP level
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    CurrentConfig.NetworkResolution = Context.ClusterOptions.EffectiveNetworkResolution;
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                else
                {
                    //A non-GCCCP cluster
#pragma warning disable CS8602 // Dereference of a possibly null reference.
                    CurrentConfig.SetEffectiveNetworkResolution(Context.ClusterOptions);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                }
                if (SupportsCollections)
                {
                    Manifest = await node.GetManifest().ConfigureAwait(false);
                }

                await node.HelloHello().ConfigureAwait(false);

                KeyMapper = _vBucketKeyMapperFactory.Create(CurrentConfig);

                Nodes.Add(node);
                await Context.ProcessClusterMapAsync(this, CurrentConfig).ConfigureAwait(false);

                var nodes = Context.GetNodes(Name);

                //update the local nodes collection
                lock (Nodes)
                {
                    foreach (var clusterNode in nodes)
                    {
                        if (!Nodes.TryGet(clusterNode.EndPoint, out _))
                        {
                            Nodes.Add(clusterNode);
                        }
                    }
                }

                ClearErrors();

                Logger.LogInformation("Bootstrapping: server negotiation completed for {name}.", Redactor.UserData(Name));
            }
            catch (Exception e)
            {
                if (e is CouchbaseException ce)
                {
                    if (ce.Context is KeyValueErrorContext {Status: ResponseStatus.NotSupported})
                    {
                        throw new NotSupportedException();
                    }
                }

                CaptureException(e);
            }

            //this needs to be started after bootstrapping has been attempted
            Bootstrapper.Start(this);
        }

        private void ClearErrors()
        {
            ((IBootstrappable)this).DeferredExceptions.Clear();
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
