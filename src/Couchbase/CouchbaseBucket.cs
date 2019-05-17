using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
    internal interface IBucketSender
    {
        Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs);

        Task Bootstrap(ClusterNode clusterNode);
    }

    public class CouchbaseBucket : IBucket, IBucketSender
    {
        internal const string DefaultScope = "_default";
        private static readonly ILogger Log = LogManager.CreateLogger<CouchbaseBucket>();
        private readonly ConcurrentDictionary<IPEndPoint, ClusterNode> _bucketNodes = new ConcurrentDictionary<IPEndPoint, ClusterNode>();
        private readonly ConcurrentDictionary<string, IScope> _scopes = new ConcurrentDictionary<string, IScope>();
        private readonly Lazy<IViewClient> _viewClientLazy;

        private bool _disposed;
        private BucketConfig _bucketConfig;
        private Manifest _manifest;
        private IKeyMapper _keyMapper;
        private readonly Configuration _configuration;
        private bool _supportsCollections;

        internal CouchbaseBucket(string name, Configuration configuration)
        {
            Name = name;
            _configuration = configuration;

            _viewClientLazy = new Lazy<IViewClient>(() =>
                new ViewClient(new CouchbaseHttpClient(_configuration, _bucketConfig), new JsonDataMapper(new DefaultSerializer()), _configuration)
            );
        }

        public string Name { get; }

        public Task<IScope> this[string name]
        {
            get
            {
                Log.LogDebug("Fetching scope {0}", name);

                if (_scopes.TryGetValue(name, out var scope))
                {
                    return Task.FromResult(scope);
                }

                throw new ScopeMissingException("Cannot locate the scope {scopeName}");
            }
        }

        public Task<ICollection> DefaultCollection => Task.FromResult(_scopes[DefaultScope][CouchbaseCollection.DefaultCollection]);

        async Task IBucketSender.Bootstrap(ClusterNode bootstrapNode)
        {
            //should never happen
            if (bootstrapNode == null)
            {
                throw new ArgumentNullException(nameof(bootstrapNode));
            }

            bootstrapNode.Owner = this;

            //reuse the bootstrapNode
            _bucketNodes.AddOrUpdate(bootstrapNode.EndPoint, bootstrapNode, (key, node) => bootstrapNode);
            bootstrapNode.Configuration = _configuration;

            //the initial bootstrapping endpoint;
            await bootstrapNode.SelectBucket(Name).ConfigureAwait(false);

            _manifest = await bootstrapNode.GetManifest().ConfigureAwait(false);
            _supportsCollections = bootstrapNode.Supports(ServerFeatures.Collections);

            _bucketConfig = await bootstrapNode.GetClusterMap().ConfigureAwait(false);//TODO this should go through standard config check process NCBC-1944
            _keyMapper = new VBucketKeyMapper(_bucketConfig);

            LoadManifest();
            await LoadClusterMap().ConfigureAwait(false);
        }

        private async Task LoadClusterMap()
        {
            foreach (var nodesExt in _bucketConfig.NodesExt)//will need to update to use "NodeAdapter" = Nodes + NodesExt like in 2.0
            {
                var endpoint = nodesExt.GetIpEndPoint(_configuration);
                if (_bucketNodes.TryGetValue(endpoint, out ClusterNode bootstrapNode))
                {
                    bootstrapNode.NodesExt = nodesExt;
                    bootstrapNode.BuildServiceUris();
                    continue; //bootstrap node is skipped because it already went through these steps
                }

                var connection = endpoint.GetConnection();
                await connection.Authenticate(_configuration, Name).ConfigureAwait(false);
                await connection.SelectBucket(Name).ConfigureAwait(false);

                //one error map per node
                var errorMap = await connection.GetErrorMap().ConfigureAwait(false);
                var supportedFeatures = await connection.Hello().ConfigureAwait(false);

                var clusterNode = new ClusterNode
                {
                    Connection = connection,
                    ErrorMap = errorMap,
                    EndPoint = endpoint,
                    ServerFeatures = supportedFeatures,
                    Configuration = _configuration,
                    NodesExt = nodesExt,

                    //build the services urls
                    QueryUri = endpoint.GetQueryUri(_configuration, nodesExt),
                    SearchUri = endpoint.GetSearchUri(_configuration, nodesExt),
                    AnalyticsUri = endpoint.GetAnalyticsUri(_configuration, nodesExt),
                    ViewsUri = endpoint.GetViewsUri(_configuration, nodesExt),
                };
                clusterNode.BuildServiceUris();
                _supportsCollections = clusterNode.Supports(ServerFeatures.Collections);
                _bucketNodes.AddOrUpdate(endpoint, clusterNode, (ep, node) => clusterNode);
                _configuration.GlobalNodes.Add(clusterNode);
            }
        }

        private void LoadManifest()
        {
            //The server supports collections so build them from the manifest
            if (_supportsCollections)
            {
                //warmup the scopes/collections and cache them
                foreach (var scopeDef in _manifest.scopes)
                {
                    var collections = new List<ICollection>();
                    foreach (var collectionDef in scopeDef.collections)
                    {
                        collections.Add(new CouchbaseCollection(this,
                            Convert.ToUInt32(collectionDef.uid), collectionDef.name));
                    }

                    _scopes.TryAdd(scopeDef.name, new Scope(scopeDef.name, scopeDef.uid, collections, this));
                }
            }
            else
            {
                //build a fake scope and collection for pre-6.5 clusters
                var defaultCollection = new CouchbaseCollection(this, null, "_default");
                var defaultScope = new Scope("_default", "0", new List<ICollection> { defaultCollection }, this);
                _scopes.TryAdd("_default", defaultScope);
            }
        }

        //TODO move Uri storage to ClusterNode - IBucket owns BucketConfig though
        private Uri GetViewUri()
        {
            var clusterNode = _configuration.GlobalNodes.GetRandom(x=>x.Owner==this && x.HasViews());
            if (clusterNode == null)
            {
                throw new ServiceMissingException("Views Service cannot be located.");
            }
            return clusterNode.ViewsUri;
        }

        public Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default)
        {
            if (options == default)
            {
                options = new ViewOptions();
            }

            // create old style query
            var query = new ViewQuery(GetViewUri().ToString())
            {
                UseSsl = _configuration.UseSsl
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

        public Task<IViewResult<T>> SpatialViewQueryAsync<T>(string designDocument, string viewName, SpatialViewOptions options = default)
        {
            if (options == default)
            {
                options = new SpatialViewOptions();
            }

            var uri = GetViewUri();

            // create old style query
            var query = new SpatialViewQuery(uri)
            {
                UseSsl = _configuration.UseSsl
            };
            query.Bucket(Name);
            query.From(designDocument, viewName);
            query.Stale(options.StaleState);
            query.Skip(options.Skip);
            query.Limit(options.Limit);
            query.StartRange(options.StartRange.ToList());
            query.EndRange(options.EndRange.ToList());
            query.Development(options.Development);
            query.ConnectionTimeout(options.ConnectionTimeout);

            return _viewClientLazy.Value.ExecuteAsync<T>(query);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (var clusterNode in _configuration.GlobalNodes.Where(x=>x.Owner==this))
            {
                clusterNode.Dispose();
            }

            //clear the local nodes but keep the global ones alive
            _bucketNodes.Clear();
            _disposed = true;
        }

        public async Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            var vBucket = (VBucket) _keyMapper.MapKey(op.Key);
            op.VBucketId = vBucket.Index;

            var endPoint = vBucket.LocatePrimary();
            await op.SendAsync(_bucketNodes[endPoint].Connection).ConfigureAwait(false);
        }
    }
}
