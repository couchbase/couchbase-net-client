using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    internal class CouchbaseBucket : BucketBase
    {
        private static readonly ILogger Log = LogManager.CreateLogger<CouchbaseBucket>();
        private readonly Lazy<IViewClient> _viewClientLazy;
        private readonly Lazy<IViewIndexManager> _viewManagerLazy;
        private readonly Lazy<ICollectionManager> _collectionManagerLazy;

        internal CouchbaseBucket(string name, ClusterContext context)
        {
            Name = name;
            Context = context;

            var httpClient = new CouchbaseHttpClient(Context);
            _viewClientLazy = new Lazy<IViewClient>(() =>
                new ViewClient(httpClient, new JsonDataMapper(new DefaultSerializer()), Context)
            );
            _viewManagerLazy = new Lazy<IViewIndexManager>(() =>
                new ViewIndexManager(name, httpClient, context));
            _collectionManagerLazy = new Lazy<ICollectionManager>(() =>
                new CollectionManager(name, context, httpClient)
            );
        }

        public override Task<IScope> this[string name]
        {
            get
            {
                Log.LogDebug("Fetching scope {0}", name);

                if (Scopes.TryGetValue(name, out var scope))
                {
                    return Task.FromResult(scope);
                }

                throw new ScopeMissingException("Cannot locate the scope {scopeName}");
            }
        }

        public override IViewIndexManager ViewIndexes => _viewManagerLazy.Value;

        /// <summary>
        /// The Collection Management API.
        /// </summary>
        /// <remarks>Volatile</remarks>
        public override ICollectionManager Collections => _collectionManagerLazy.Value;

        internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            if (e.Config.Name == Name && e.Config.Rev > BucketConfig.Rev)
            {
                BucketConfig = e.Config;
                if (BucketConfig.VBucketMapChanged)
                {
                    KeyMapper = new VBucketKeyMapper(BucketConfig);
                }
                if (BucketConfig.ClusterNodesChanged)
                {
                    Task.Run(async () => await Context.ProcessClusterMapAsync(this, BucketConfig)).GetAwaiter().GetResult();
                }
            }
        }

        //TODO move Uri storage to ClusterNode - IBucket owns BucketConfig though
        private Uri GetViewUri()
        {
            var clusterNode = Context.GetRandomNodeForService(ServiceType.Views, Name);
            if (clusterNode == null)
            {
                throw new ServiceMissingException("Views Service cannot be located.");
            }
            return clusterNode.ViewsUri;
        }

        public override Task<IViewResult> ViewQueryAsync(string designDocument, string viewName, ViewOptions options = null)
        {
            options = options ?? new ViewOptions();
            // create old style query
            var query = new ViewQuery(GetViewUri().ToString())
            {
                UseSsl = Context.ClusterOptions.EnableTls
            };

            //Normalize to new naming convention for public API RFC#51
            var staleState = StaleState.None;
            if (options.ScanConsistency == ViewScanConsistency.RequestPlus)
            {
                staleState = StaleState.False;
            }
            if (options.ScanConsistency == ViewScanConsistency.UpdateAfter)
            {
                staleState = StaleState.UpdateAfter;
            }
            if (options.ScanConsistency == ViewScanConsistency.NotBounded)
            {
                staleState = StaleState.Ok;
            }

            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(staleState);
            query.Limit(options.Limit);
            query.Skip(options.Skip);
            query.StartKey(options.StartKey);
            query.StartKeyDocId(options.StartKeyDocId);
            query.EndKey(options.EndKey);
            query.EndKeyDocId(options.EndKeyDocId);
            query.InclusiveEnd(options.InclusiveEnd);
            query.Group(options.Group);
            query.GroupLevel(options.GroupLevel);
            query.Key(options.Key);
            query.Keys(options.Keys);
            query.Reduce(options.Reduce);
            query.Development(options.Development);
            query.ConnectionTimeout(options.ConnectionTimeout);
            query.Debug(options.Debug);
            query.Namespace(options.Namespace);
            query.OnError(options.OnError == ViewErrorMode.Stop);

            if (options.ViewOrdering == ViewOrdering.Decesending)
            {
                query.Desc();
            }
            else
            {
                query.Asc();
            }

            if (options.FullSet.HasValue && options.FullSet.Value)
            {
                query.FullSet();
            }

            foreach (var kvp in options.RawParameters)
            {
                query.Raw(kvp.Key, kvp.Value);
            }

            return _viewClientLazy.Value.ExecuteAsync(query);
        }

        internal override async Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
        {
            var vBucket = (VBucket) KeyMapper.MapKey(op.Key);
            var endPoint = op.VBucketId.HasValue ?
                vBucket.LocateReplica(op.VBucketId.Value) :
                vBucket.LocatePrimary();

            op.VBucketId = vBucket.Index;

            if (Context.TryGetNode(endPoint, out var clusterNode))
            {
                await clusterNode.SendAsync(op, token, timeout);
            }
            else
            {
               throw new NodeNotAvailableException($"Cannot find a Couchbase Server node for {endPoint}.");
            }
        }

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            node.Owner = this;
            await node.SelectBucket(this.Name);

            if (Context.SupportsCollections)
            {
                Manifest = await node.GetManifest().ConfigureAwait(false);
            }
            //we still need to add a default collection
            LoadManifest();

            BucketConfig = await node.GetClusterMap().ConfigureAwait(false);
            KeyMapper = new VBucketKeyMapper(BucketConfig);

            await Context.ProcessClusterMapAsync(this, BucketConfig);
        }
    }
}
