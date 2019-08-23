using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.DataMapping;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.IO.Serializers;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.Services.Views;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    internal class CouchbaseBucket : BucketBase
    {
        private static readonly ILogger Log = LogManager.CreateLogger<CouchbaseBucket>();
        private readonly Lazy<IViewClient> _viewClientLazy;
        private readonly Lazy<IViewManager> _viewManagerLazy;

        internal CouchbaseBucket(string name, ClusterOptions clusterOptions, ConfigContext couchbaseContext)
        {
            Name = name;
            CouchbaseContext = couchbaseContext;
            ClusterOptions = clusterOptions;
            CouchbaseContext.Subscribe(this);

            var httpClient = new CouchbaseHttpClient(ClusterOptions);
            _viewClientLazy = new Lazy<IViewClient>(() =>
                new ViewClient(httpClient, new JsonDataMapper(new DefaultSerializer()), ClusterOptions)
            );
            _viewManagerLazy = new Lazy<IViewManager>(() =>
                new ViewManager(name, httpClient, clusterOptions));
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

        public override IViewManager ViewIndexes => _viewManagerLazy.Value;

        protected override void LoadManifest()
        {
            //The server supports collections so build them from the manifest
            if (SupportsCollections)
            {
                //warmup the scopes/collections and cache them
                foreach (var scopeDef in Manifest.scopes)
                {
                    var collections = new List<ICollection>();
                    foreach (var collectionDef in scopeDef.collections)
                    {
                        collections.Add(new CouchbaseCollection(this,
                            Convert.ToUInt32(collectionDef.uid), collectionDef.name));
                    }

                    Scopes.TryAdd(scopeDef.name, new Scope(scopeDef.name, scopeDef.uid, collections, this));
                }
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters
                var defaultCollection = new CouchbaseCollection(this, null, "_default");
                var defaultScope = new Scope("_default", "0", new List<ICollection> { defaultCollection }, this);
                Scopes.TryAdd("_default", defaultScope);
            }
        }

        //TODO move Uri storage to ClusterNode - IBucket owns BucketConfig though
        private Uri GetViewUri()
        {
            var clusterNode = ClusterOptions.GlobalNodes.GetRandom(x=>x.Owner==this && x.HasViews());
            if (clusterNode == null)
            {
                throw new ServiceMissingException("Views Service cannot be located.");
            }
            return clusterNode.ViewsUri;
        }

        public override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default)
        {
            if (options == default)
            {
                options = new ViewOptions();
            }

            // create old style query
            var query = new ViewQuery(GetViewUri().ToString())
            {
                UseSsl = ClusterOptions.UseSsl
            };
            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(options.StaleState);
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
            query.GroupLevel(options.GroupLevel);
            query.Reduce(options.Reduce);
            query.Development(options.Development);
            query.ConnectionTimeout(options.ConnectionTimeout);

            if (options.Descending.HasValue)
            {
                if (options.Descending.Value)
                {
                    query.Desc();
                }
                else
                {
                    query.Asc();
                }
            }

            if (options.FullSet.HasValue && options.FullSet.Value)
            {
                query.FullSet();
            }

            return _viewClientLazy.Value.ExecuteAsync<T>(query);
        }

        internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            if (e.Config.Name == Name &&  e.Config.Rev > BucketConfig.Rev)
            {
                BucketConfig = e.Config;
                if (BucketConfig.VBucketMapChanged)
                {
                    KeyMapper = new VBucketKeyMapper(BucketConfig);
                }
                if (BucketConfig.ClusterNodesChanged)
                {
                    LoadClusterMap(BucketConfig.GetNodes()).ConfigureAwait(false).GetAwaiter().GetResult();
                    Prune(BucketConfig);
                }
            }
        }

        internal override async Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            var vBucket = (VBucket) KeyMapper.MapKey(op.Key);
            op.VBucketId = vBucket.Index;

            var endPoint = vBucket.LocatePrimary();
            var clusterNode = BucketNodes[endPoint];
            await CheckConnection(clusterNode).ConfigureAwait(false);
            await op.SendAsync(clusterNode.Connection).ConfigureAwait(false);
        }

        internal override async Task Bootstrap(params IClusterNode[] bootstrapNodes)
        {
            //should never happen
            if (bootstrapNodes == null)
            {
                throw new ArgumentNullException(nameof(bootstrapNodes));
            }

            List<NodeAdapter> nodeAdapters = null;
            var bootstrapNode = bootstrapNodes.First();

            //reuse the bootstrapNode
            BucketNodes.AddOrUpdate(bootstrapNode.EndPoint, bootstrapNode, (key, node) => bootstrapNode);
            bootstrapNode.ClusterOptions = ClusterOptions;

            //the initial bootstrapping endpoint;
            await bootstrapNode.SelectBucket(Name).ConfigureAwait(false);

            Manifest = await bootstrapNode.GetManifest().ConfigureAwait(false);
            SupportsCollections = bootstrapNode.Supports(ServerFeatures.Collections);

            BucketConfig = await bootstrapNode.GetClusterMap().ConfigureAwait(false);
            KeyMapper = new VBucketKeyMapper(BucketConfig);

            nodeAdapters = BucketConfig.GetNodes();
            if (nodeAdapters.Count == 1)
            {
                var nodeAdapter = nodeAdapters.First();
                bootstrapNode.NodesAdapter = nodeAdapter;
            }
            else
            {
                bootstrapNode.NodesAdapter =
                    nodeAdapters.Find(x => x.Hostname == bootstrapNode.BootstrapUri.Host);
            }

            LoadManifest();
            LoadClusterMap(nodeAdapters).ConfigureAwait(false).GetAwaiter().GetResult();
            bootstrapNode.Owner = this;
        }
    }
}
