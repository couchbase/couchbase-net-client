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
using Couchbase.KeyValue;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

namespace Couchbase
{
    internal class MemcachedBucket : BucketBase
    {
        private static readonly ILogger Log = LogManager.CreateLogger<MemcachedBucket>();
        private readonly HttpClusterMapBase _httpClusterMap;

        internal MemcachedBucket(string name, ClusterContext context) :
            this(name, context, new HttpClusterMap(new CouchbaseHttpClient(context), context))
        {
        }

        internal MemcachedBucket(string name, ClusterContext context, HttpClusterMapBase httpClusterMap)
        {
            Name = name;
            _httpClusterMap = httpClusterMap;
            SupportsCollections = false;
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

        public  override Task<IViewResult> ViewQueryAsync(string designDocument, string viewName, ViewOptions options = default)
        {
            throw new NotSupportedException("Views are not supported by Memcached Buckets.");
        }

        public override IViewIndexManager Views =>  throw new NotSupportedException("View Indexes are not supported by Memcached Buckets.");

        public override ICollectionManager Collections =>  throw new NotSupportedException("Collections are not supported by Memcached Buckets.");

        internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            if (e.Config.Name == Name && e.Config.Rev > BucketConfig.Rev)
            {
                BucketConfig = e.Config;
                KeyMapper = new KetamaKeyMapper(BucketConfig, Context.ClusterOptions);

                if (BucketConfig.ClusterNodesChanged)
                {
                    Task.Run(async () => await Context.ProcessClusterMapAsync(this, BucketConfig));
                }
            }
        }

        internal override async Task SendAsync(IOperation op, CancellationToken token = default, TimeSpan? timeout = null)
        {
            var bucket = KeyMapper.MapKey(op.Key);
            var endPoint = bucket.LocatePrimary();

            if (Context.TryGetNode(endPoint, out var clusterNode))
            {
                await clusterNode.ExecuteOp(op, token, timeout);
            }
            else
            {
                //raise exceptin that node is not found
            }
        }

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            node.Owner = this;
            //fetch the cluster map to avoid race condition with streaming http
            BucketConfig = await _httpClusterMap.GetClusterMapAsync(
                Name, node.BootstrapUri, CancellationToken.None).ConfigureAwait(false);

            KeyMapper = new KetamaKeyMapper(BucketConfig, Context.ClusterOptions);

            //the initial bootstrapping endpoint;
            await node.SelectBucket(Name).ConfigureAwait(false);

            LoadManifest();
            await Task.Run(async () => await Context.ProcessClusterMapAsync(this, BucketConfig));
        }
    }
}
