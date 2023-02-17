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
        private readonly IHttpClusterMapFactory _httpClusterMapFactory;
        private readonly HttpClusterMapBase _httpClusterMap;
        private readonly SemaphoreSlim _configMutex = new(1, 1);

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<MemcachedBucket> logger, TypedRedactor redactor, IBootstrapperFactory bootstrapperFactory, IRequestTracer tracer, IOperationConfigurator operationConfigurator, IRetryStrategy retryStrategy, IHttpClusterMapFactory httpClusterMapFactory, BucketConfig config) :
            this(name, context, scopeFactory, retryOrchestrator, ketamaKeyMapperFactory, logger,
                httpClusterMapFactory, redactor, bootstrapperFactory, tracer, operationConfigurator, retryStrategy, config)
        {
        }

        internal MemcachedBucket(string name, ClusterContext context, IScopeFactory scopeFactory, IRetryOrchestrator retryOrchestrator, IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<MemcachedBucket> logger, IHttpClusterMapFactory httpClusterMapFactory, TypedRedactor redactor, IBootstrapperFactory bootstrapperFactory, IRequestTracer tracer, IOperationConfigurator operationConfigurator, IRetryStrategy retryStrategy, BucketConfig config)
            : base(name, context, scopeFactory, retryOrchestrator, logger, redactor, bootstrapperFactory, tracer, operationConfigurator, retryStrategy, config)
        {
            BucketType = BucketType.Memcached;
            Name = name;
            _ketamaKeyMapperFactory = ketamaKeyMapperFactory ?? throw new ArgumentNullException(nameof(ketamaKeyMapperFactory));
            _httpClusterMapFactory = httpClusterMapFactory ?? throw new ArgumentNullException(nameof(httpClusterMapFactory));
            _httpClusterMap = _httpClusterMapFactory.Create(Context);
        }

        public override IScope Scope(string scopeName)
        {
            if (scopeName == KeyValue.Scope.DefaultScopeName)
            {
                // Base will do the logging
                return base.Scope(scopeName);
            }

            // Log here so we have info when we hit the exception path
            Logger.LogDebug("Fetching scope {scopeName}", Redactor.MetaData(scopeName));
            throw new NotSupportedException("Only the default Scope is supported by Memcached Buckets");
        }

        /// <inheritdoc />
        public  override Task<IViewResult<TKey, TValue>> ViewQueryAsync<TKey, TValue>(string designDocument, string viewName, ViewOptions? options = default)
        {
            throw new NotSupportedException("Views are not supported by Memcached Buckets.");
        }

        public override IViewIndexManager ViewIndexes => throw new NotSupportedException("View Indexes are not supported by Memcached Buckets.");

        public override ICouchbaseCollectionManager Collections => throw new NotSupportedException("Collections are not supported by Memcached Buckets.");

        public override async Task ConfigUpdatedAsync(BucketConfig newConfig)
        {
            using var cts = new CancellationTokenSource(Context.ClusterOptions.ConfigUpdatingTimeout);
            await _configMutex.WaitAsync(cts.Token).ConfigureAwait(false);
            try
            {
                if (newConfig.HasConfigChanges(CurrentConfig, Name))
                {
                    KeyMapper = _ketamaKeyMapperFactory.Create(newConfig);

                    if (newConfig.HasClusterNodesChanged(CurrentConfig) || newConfig.IgnoreRev)
                    {
                        await Context.ProcessClusterMapAsync(this, newConfig).ConfigureAwait(false);
                    }
                    CurrentConfig = newConfig;
                }
            }
            finally
            {
                _configMutex.Release();
            }
        }

        internal override Task<ResponseStatus> SendAsync(IOperation op, CancellationTokenPair tokenPair = default)
        {
            if (KeyMapper == null)
            {
                throw new InvalidOperationException("Bucket is not bootstrapped.");
            }

            var bucket = KeyMapper.MapKey(op.Key);
            var endPoint = bucket.LocatePrimary().GetValueOrDefault();

            try
            {
                if (Nodes.TryGet(endPoint, out var clusterNode))
                {
                    return clusterNode.ExecuteOp(op, tokenPair);
                }
                else
                {
                    throw new NodeNotAvailableException(
                           $"Cannot find a Couchbase Server node for {endPoint}.");
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

        internal override async Task BootstrapAsync(IClusterNode node)
        {
            try
            {
                //fetch the cluster map to avoid race condition with streaming http
                CurrentConfig = await _httpClusterMap.GetClusterMapAsync(
                    Name, node.EndPoint, CancellationToken.None).ConfigureAwait(false);

                if (Context.ClusterOptions.HasNetworkResolution)
                {
                    //Network resolution determined at the GCCCP level
                    CurrentConfig.NetworkResolution = Context.ClusterOptions.EffectiveNetworkResolution;
                }
                else
                {
                    //A non-GCCCP cluster
                    CurrentConfig.SetEffectiveNetworkResolution(Context.ClusterOptions);
                }

                KeyMapper = _ketamaKeyMapperFactory.Create(CurrentConfig);

                node.Owner = this;
                Nodes.Add(node);
                await Context.ProcessClusterMapAsync(this, CurrentConfig).ConfigureAwait(false);
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
