using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DI;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Retry;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase
{
    internal class CouchbaseBucket : BucketBase
    {
        private readonly IVBucketKeyMapperFactory _vBucketKeyMapperFactory;
        private readonly Lazy<IViewClient> _viewClientLazy;
        private readonly Lazy<IViewIndexManager> _viewManagerLazy;
        private readonly Lazy<ICollectionManager> _collectionManagerLazy;

        internal CouchbaseBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory, ILogger<CouchbaseBucket> logger)
            : base(name, context, scopeFactory, retryOrchestrator, logger)
        {
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));

            _viewClientLazy = new Lazy<IViewClient>(() =>
                context.ServiceProvider.GetRequiredService<IViewClient>()
            );
            _viewManagerLazy = new Lazy<IViewIndexManager>(() =>
                new ViewIndexManager(name,
                    context.ServiceProvider.GetRequiredService<IServiceUriProvider>(),
                    context.ServiceProvider.GetRequiredService<CouchbaseHttpClient>(),
                    context.ServiceProvider.GetRequiredService<ILogger<ViewIndexManager>>()));
            _collectionManagerLazy = new Lazy<ICollectionManager>(() =>
                new CollectionManager(name,
                    context.ServiceProvider.GetRequiredService<IServiceUriProvider>(),
                    context.ServiceProvider.GetRequiredService<CouchbaseHttpClient>(),
                    context.ServiceProvider.GetRequiredService<ILogger<CollectionManager>>())
            );
        }

        public override IScope this[string scopeName]
        {
            get
            {
                Logger.LogDebug("Fetching scope {scopeName}", scopeName);

                if (Scopes.TryGetValue(scopeName, out var scope))
                {
                    return scope;
                }

                throw new ScopeNotFoundException(scopeName);
            }
        }

        public override IViewIndexManager ViewIndexes => _viewManagerLazy.Value;

        /// <summary>
        /// The Collection Management API.
        /// </summary>
        /// <remarks>Volatile</remarks>
        public override ICollectionManager Collections => _collectionManagerLazy.Value;

        public override async Task ConfigUpdatedAsync(BucketConfig config)
        {
            if (config.Name == Name && (BucketConfig == null || config.Rev > BucketConfig.Rev))
            {
                BucketConfig = config;

                if (BucketConfig.VBucketMapChanged)
                {
                    KeyMapper = await _vBucketKeyMapperFactory.CreateAsync(BucketConfig).ConfigureAwait(false);
                }

                if (BucketConfig.ClusterNodesChanged)
                {
                    await Context.ProcessClusterMapAsync(this, BucketConfig).ConfigureAwait(false);
                }
            }
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
                UseSsl = Context.ClusterOptions.EnableTls
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

        internal override async Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
        {
            if (KeyMapper == null)
            {
                throw new InvalidOperationException("Bucket is not bootstrapped.");
            }

            var vBucket = (VBucket) KeyMapper.MapKey(op.Key);
            var endPoint = op.VBucketId.HasValue ?
                vBucket.LocateReplica(op.VBucketId.Value) :
                vBucket.LocatePrimary();

            op.VBucketId = vBucket.Index;

            if (Context.TryGetNode(endPoint, out var clusterNode))
            {
                try
                {
                    await clusterNode.SendAsync(op, token, timeout);
                }
                catch (Exception e)
                {
                    if (e is CollectionOutdatedException)
                    {
                        Logger.LogInformation("Updating stale manifest for collection and retrying.", e);
                        await RefreshCollectionId(op, clusterNode);
                        await clusterNode.SendAsync(op, token, timeout);
                    }
                    else
                    {
                        throw;//propagate up
                    }
                }
            }
            else
            {
               throw new NodeNotAvailableException($"Cannot find a Couchbase Server node for {endPoint}.");
            }
        }

        private async Task RefreshCollectionId(IOperation op, IClusterNode node)
        {
            var scope = Scope(op.SName);
            var collection = (CouchbaseCollection)scope.Collection(op.CName);
            var newCid = await node.GetCid($"{op.SName}.{op.CName}");
            collection.Cid = newCid;
            op.Cid = collection.Cid;
        }

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            try
            {
                await node.SelectBucketAsync(this).ConfigureAwait(false);

                if (Context.SupportsCollections)
                {
                    Manifest = await node.GetManifest().ConfigureAwait(false);
                }

                //we still need to add a default collection
                LoadManifest();

                BucketConfig = await node.GetClusterMap().ConfigureAwait(false);
                KeyMapper = await _vBucketKeyMapperFactory.CreateAsync(BucketConfig).ConfigureAwait(false);

                await Context.ProcessClusterMapAsync(this, BucketConfig);
            }
            catch (Exception e)
            {
                CaptureException(e);
            }
        }
    }
}
