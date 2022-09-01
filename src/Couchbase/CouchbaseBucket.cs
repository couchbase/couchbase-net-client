using System;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Utils;
using Couchbase.Views;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

#nullable enable

namespace Couchbase
{
    internal class CouchbaseBucket : BucketBase
    {
        private readonly IVBucketKeyMapperFactory _vBucketKeyMapperFactory;
        private readonly Lazy<IViewClient> _viewClientLazy;
        private readonly Lazy<IViewIndexManager> _viewManagerLazy;
        private readonly Lazy<ICouchbaseCollectionManager> _collectionManagerLazy;

        internal CouchbaseBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory, ILogger<CouchbaseBucket> logger, TypedRedactor redactor, IBootstrapperFactory bootstrapperFactory,
            IRequestTracer tracer, IOperationConfigurator operationConfigurator, IRetryStrategy retryStrategy, BucketConfig config)
            : base(name, context, scopeFactory, retryOrchestrator, logger, redactor, bootstrapperFactory, tracer, operationConfigurator, retryStrategy, config)
        {
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));

            _viewClientLazy = new Lazy<IViewClient>(() =>
                context.ServiceProvider.GetRequiredService<IViewClient>()
            );
            _viewManagerLazy = new Lazy<IViewIndexManager>(() =>
                new ViewIndexManager(name,
                    context.ServiceProvider.GetRequiredService<IServiceUriProvider>(),
                    context.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>(),
                    context.ServiceProvider.GetRequiredService<ILogger<ViewIndexManager>>(),
                    context.ServiceProvider.GetRequiredService<IRedactor>()));

            _collectionManagerLazy = new Lazy<ICouchbaseCollectionManager>(() =>
                new CollectionManager(name,
                    context.ServiceProvider.GetRequiredService<IServiceUriProvider>(),
                    context.ServiceProvider.GetRequiredService<ICouchbaseHttpClientFactory>(),
                    context.ServiceProvider.GetRequiredService<ILogger<CollectionManager>>(),
                    context.ServiceProvider.GetRequiredService<IRedactor>())
            );
        }

        public override IViewIndexManager ViewIndexes => _viewManagerLazy.Value;

        /// <summary>
        /// The Collection Management API.
        /// </summary>
        /// <remarks>Volatile</remarks>
        public override ICouchbaseCollectionManager Collections => _collectionManagerLazy.Value;

        public override async Task ConfigUpdatedAsync(BucketConfig newConfig)
        {
            if (newConfig.Name == Name && newConfig.IsNewerThan(CurrentConfig))
            {
                if (Logger.IsEnabled(LogLevel.Debug))
                {
                    Logger.LogDebug("Processing cluster map for rev#{revision} on {bucketName} - old rev#{oldRevision}",
                        newConfig.Rev, Name, CurrentConfig?.Rev);
                    Logger.LogDebug(JsonSerializer.Serialize(CurrentConfig, InternalSerializationContext.Default.BucketConfig!));
                }

                CurrentConfig = newConfig;
                if (CurrentConfig.VBucketMapChanged || newConfig.IgnoreRev)
                {
                    Logger.LogDebug(LoggingEvents.ConfigEvent, "Updating VB key mapper for rev#{revision} on {bucketName}", newConfig.Rev, Name);
                    KeyMapper = _vBucketKeyMapperFactory.Create(CurrentConfig);
                }

                if (CurrentConfig.ClusterNodesChanged || newConfig.IgnoreRev)
                {
                    Logger.LogDebug(LoggingEvents.ConfigEvent, "Updating cluster nodes for rev#{revision} on {bucketName}", newConfig.Rev, Name);
                    await Context.ProcessClusterMapAsync(this, CurrentConfig).ConfigureAwait(false);
                    var nodes = Context.GetNodes(Name);

                    //update the local nodes collection
                    lock (Nodes)
                    {
                        Nodes.Clear();
                        foreach (var clusterNode in nodes)
                        {
                            Nodes.Add(clusterNode);
                        }
                    }
                }
            }
            Logger.LogDebug("Current revision for {bucketName} is rev#{revision}", Name, CurrentConfig?.Rev);
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
                var client1 = _viewClientLazy.Value;
                return await client1.ExecuteAsync<TKey, TValue>(query).ConfigureAwait(false);
            }

            return await RetryOrchestrator.RetryAsync(Func, query).ConfigureAwait(false);
        }

        internal override async Task SendAsync(IOperation op, CancellationTokenPair tokenPair = default)
        {
            if (KeyMapper == null) ThrowHelper.ThrowInvalidOperationException($"Bucket {Name} is not bootstrapped.");

            if (op.RequiresVBucketId)
            {
                var vBucket = (VBucket) KeyMapper.MapKey(op.Key, op.WasNmvb());

                var endPoint = op.ReplicaIdx != null && op.ReplicaIdx > -1
                    ? vBucket.LocateReplica(op.ReplicaIdx.GetValueOrDefault())
                    : vBucket.LocatePrimary();

                op.VBucketId = vBucket.Index;

                try
                {
                    if (Nodes.TryGet(endPoint.GetValueOrDefault(), out var clusterNode))
                    {
                        await clusterNode.SendAsync(op, tokenPair).ConfigureAwait(false);
                        return;
                    }
                }
                catch (ArgumentNullException)
                {
                    //We could not find a candidate node to send so put into the retry queue
                    //its likely were between cluster map updates and we'll try again later
                    throw new NodeNotAvailableException(
                        $"Cannot find a Couchbase Server node for {endPoint}.");
                }
            }

            //Make sure we use a node with the data service
            var node = Nodes.GetRandom(x => x.HasKv);
            if (node == null)
                throw new NodeNotAvailableException(
                    $"Cannot find a Couchbase Server node for executing {op.GetType()}.");
            await node.SendAsync(op, tokenPair).ConfigureAwait(false);
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
