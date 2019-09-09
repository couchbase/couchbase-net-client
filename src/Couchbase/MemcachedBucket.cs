using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations.Legacy;
using Couchbase.Core.Logging;
using Couchbase.Core.Sharding;
using Couchbase.Management;
using Couchbase.Services.Views;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    internal class MemcachedBucket : BucketBase
    {
        private static readonly ILogger Log = LogManager.CreateLogger<MemcachedBucket>();
        private readonly HttpClusterMapBase _httpClusterMap;

        internal MemcachedBucket(string name, ClusterOptions clusterOptions, ConfigContext couchbaseContext) :
            this (name, clusterOptions, couchbaseContext, new HttpClusterMap(new CouchbaseHttpClient(clusterOptions), couchbaseContext, clusterOptions))
        {
        }

        internal MemcachedBucket(string name, ClusterOptions clusterOptions, ConfigContext couchbaseContext, HttpClusterMapBase httpClusterMap)
        {
            Name = name;
            CouchbaseContext = couchbaseContext;
            ClusterOptions = clusterOptions;
            CouchbaseContext.Subscribe(this);
            _httpClusterMap = httpClusterMap;
        }

        public override Task<IScope> this[string name]
        {
            get
            {
                Log.LogDebug("Fetching scope {0}", name);

                if (name == DefaultScopeName)
                    if (Scopes.TryGetValue(name, out var scope))
                        return Task.FromResult(scope);

                throw new NotSupportedException("Only the default Scope is supported by Memcached Buckets");
            }
        }

        public  override Task<IViewResult<T>> ViewQueryAsync<T>(string designDocument, string viewName, ViewOptions options = default)
        {
            throw new NotSupportedException("Views are not supported by Memcached Buckets.");
        }

        public override IViewManager ViewIndexes =>  throw new NotSupportedException("View Indexes are not supported by Memcached Buckets.");

        public override ICollectionManager Collections =>  throw new NotSupportedException("Collections are not supported by Memcached Buckets.");

        protected override void LoadManifest()
        {
            //this condition should never be hit
            if (SupportsCollections)
            {
                throw new NotSupportedException("Only the default collection is supported by Memcached buckets.");
            }

            //build a fake scope and collection for Memcached buckets which do not support scopes or collections
            var defaultCollection = new CouchbaseCollection(this, null, "_default");
            var defaultScope = new Scope("_default", "0", new List<ICollection> {defaultCollection}, this);
            Scopes.TryAdd("_default", defaultScope);
        }

        internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            if (e.Config.Name == Name && e.Config.Rev > BucketConfig.Rev)
            {
                BucketConfig = e.Config;
                KeyMapper = new KetamaKeyMapper(BucketConfig, ClusterOptions);

                if (BucketConfig.ClusterNodesChanged)
                {
                    LoadClusterMap(BucketConfig.GetNodes()).ConfigureAwait(false).GetAwaiter().GetResult();
                    Prune(BucketConfig);
                }
            }
        }

        internal override async Task Send(IOperation op, TaskCompletionSource<IMemoryOwner<byte>> tcs)
        {
            var bucket = KeyMapper.MapKey(op.Key);
            var endPoint = bucket.LocatePrimary();
            var clusterNode = BucketNodes[endPoint];
            await CheckConnection(clusterNode).ConfigureAwait(false);
            await op.SendAsync(clusterNode.Connection).ConfigureAwait(false);
        }

        internal override async Task Bootstrap(params IClusterNode[] bootstrapNodes)
        {
            var bootstrapNode = bootstrapNodes.FirstOrDefault();

            //fetch the cluster map to avoid race condition with streaming http
            BucketConfig = await _httpClusterMap.GetClusterMapAsync(
                Name, bootstrapNode.BootstrapUri, CancellationToken.None).ConfigureAwait(false);

            KeyMapper = new KetamaKeyMapper(BucketConfig, ClusterOptions);

            //reuse the bootstrapNode
            BucketNodes.AddOrUpdate(bootstrapNode.EndPoint, bootstrapNode, (key, node) => bootstrapNode);
            bootstrapNode.ClusterOptions = ClusterOptions;

            //the initial bootstrapping endpoint;
            await bootstrapNode.SelectBucket(Name).ConfigureAwait(false);

            LoadManifest();
            LoadClusterMap(BucketConfig.GetNodes()).ConfigureAwait(false).GetAwaiter().GetResult();
            bootstrapNode.Owner = this;
        }
    }
}
