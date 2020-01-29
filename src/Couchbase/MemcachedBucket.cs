using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
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
    internal class MemcachedBucket : BucketBase
    {
        private readonly HttpClusterMapBase _httpClusterMap;

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, ILogger<MemcachedBucket> logger) :
            this(name, context, scopeFactory, retryOrchestrator, logger, new HttpClusterMap(new CouchbaseHttpClient(context), context))
        {
        }

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, ILogger<MemcachedBucket> logger,
            HttpClusterMapBase httpClusterMap)
            : base(name, context, scopeFactory, retryOrchestrator, logger)
        {
            Name = name;
            _httpClusterMap = httpClusterMap;
        }

        public override IScope Scope(string scopeName)
        {
            return this[scopeName];
        }

        public override IScope this[string scopeName]
        {
            get
            {

                Logger.LogDebug("Fetching scope {0}", scopeName);

                if (scopeName == KeyValue.Scope.DefaultScopeName)
                    if (Scopes.TryGetValue(scopeName, out var scope))
                        return scope;

                throw new NotSupportedException("Only the default Scope is supported by Memcached Buckets");
            }
        }

        /// <inheritdoc />
        public  override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = default)
        {
            throw new NotSupportedException("Views are not supported by Memcached Buckets.");
        }

        public override IViewIndexManager ViewIndexes => throw new NotSupportedException("View Indexes are not supported by Memcached Buckets.");

        public override ICollectionManager Collections => throw new NotSupportedException("Collections are not supported by Memcached Buckets.");

        internal override void ConfigUpdated(object sender, BucketConfigEventArgs e)
        {
            if (e.Config.Name == Name && (BucketConfig ==  null || e.Config.Rev > BucketConfig.Rev))
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
            if (KeyMapper == null)
            {
                throw new InvalidOperationException("Bucket is not bootstrapped.");
            }

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
