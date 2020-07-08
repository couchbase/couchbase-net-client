using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.DI;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.HTTP;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;
using Couchbase.Management.Buckets;
using Couchbase.Management.Collections;
using Couchbase.Management.Views;
using Couchbase.Views;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase
{
    internal class MemcachedBucket : BucketBase
    {
        private readonly IKetamaKeyMapperFactory _ketamaKeyMapperFactory;
        private readonly HttpClusterMapBase _httpClusterMap;

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<MemcachedBucket> logger, IRedactor redactor, IBootstrapperFactory bootstrapperFactory, IRequestTracer tracer) :
            this(name, context, scopeFactory, retryOrchestrator, ketamaKeyMapperFactory, logger,
                new HttpClusterMap(context.ServiceProvider.GetRequiredService<CouchbaseHttpClient>(), context), redactor, bootstrapperFactory, tracer)
        {
        }

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<MemcachedBucket> logger, HttpClusterMapBase httpClusterMap, IRedactor redactor, IBootstrapperFactory bootstrapperFactory, IRequestTracer tracer)
            : base(name, context, scopeFactory, retryOrchestrator, logger, redactor, bootstrapperFactory, tracer)
        {
            BucketType = BucketType.Memcached;
            Name = name;
            _ketamaKeyMapperFactory = ketamaKeyMapperFactory ?? throw new ArgumentNullException(nameof(ketamaKeyMapperFactory));
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
                Logger.LogDebug("Fetching scope {scopeName}", Redactor.MetaData(scopeName));

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

        public override ICouchbaseCollectionManager Collections => throw new NotSupportedException("Collections are not supported by Memcached Buckets.");

        public override async Task ConfigUpdatedAsync(BucketConfig config)
        {
            if (config.Name == Name && (BucketConfig == null || config.Rev > BucketConfig.Rev))
            {
                BucketConfig = config;

                KeyMapper = await _ketamaKeyMapperFactory.CreateAsync(BucketConfig).ConfigureAwait(false);

                if (BucketConfig.ClusterNodesChanged)
                {
                    await Context.ProcessClusterMapAsync(this, BucketConfig).ConfigureAwait(false);
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

            if (Nodes.TryGet(endPoint, out var clusterNode))
            {
                await clusterNode.ExecuteOp(op, token, timeout).ConfigureAwait(false);
            }
            else
            {
                //raise exception that node is not found
            }
        }

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            try
            {
                //the initial bootstrapping endpoint;
                await node.SelectBucketAsync(this).ConfigureAwait(false);

                //fetch the cluster map to avoid race condition with streaming http
                BucketConfig = await _httpClusterMap.GetClusterMapAsync(
                    Name, node.BootstrapEndpoint, CancellationToken.None).ConfigureAwait(false);

                KeyMapper = await _ketamaKeyMapperFactory.CreateAsync(BucketConfig).ConfigureAwait(false);

                node.Owner = this;
                LoadManifest();
                await Context.ProcessClusterMapAsync(this, BucketConfig).ConfigureAwait(false);
            }
            catch (CouchbaseException e)
            {
                Logger.LogDebug(LoggingEvents.BootstrapEvent, e, "");
                throw;
            }

            //If we cannot bootstrap initially will loop and retry again.
            Bootstrapper.Start(this);
        }
    }
}
