using System;
using Couchbase.Core.Bootstrapping;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Configuration.Server.Streaming;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.Logging;
using Couchbase.Core.Retry;
using Couchbase.Management.Buckets;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates a BucketBase class based on <seealso cref="BucketType"/>.
    /// </summary>
    internal class BucketFactory : IBucketFactory
    {
        private readonly ClusterContext _clusterContext;
        private readonly IScopeFactory _scopeFactory;
        private readonly IRetryOrchestrator _retryOrchestrator;
        private readonly IVBucketKeyMapperFactory _vBucketKeyMapperFactory;
        private readonly IKetamaKeyMapperFactory _ketamaKeyMapperFactory;
        private readonly ILogger<CouchbaseBucket> _couchbaseLogger;
        private readonly ILogger<MemcachedBucket> _memcachedLogger;
        private readonly TypedRedactor _redactor;
        private readonly IBootstrapperFactory _bootstrapperFactory;
        private readonly IRequestTracer _tracer;
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly IRetryStrategy _retryStrategy;
        private readonly IHttpClusterMapFactory _httpClusterMapFactory;
        private readonly IConfigPushHandlerFactory _configPushHandlerFactory;

        public BucketFactory(
            ClusterContext clusterContext,
            IScopeFactory scopeFactory,
            IRetryOrchestrator retryOrchestrator,
            IVBucketKeyMapperFactory vBucketKeyMapperFactory,
            IKetamaKeyMapperFactory ketamaKeyMapperFactory,
            ILogger<CouchbaseBucket> couchbaseLogger,
            ILogger<MemcachedBucket> memcachedLogger,
            TypedRedactor redactor,
            IBootstrapperFactory bootstrapperFactory,
            IRequestTracer tracer,
            IOperationConfigurator operationConfigurator,
            IRetryStrategy retryStrategy,
            IHttpClusterMapFactory httpClusterMapFactory,
            IConfigPushHandlerFactory configPushHandlerFactory
            )
        {
            _clusterContext = clusterContext ?? throw new ArgumentNullException(nameof(clusterContext));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _retryOrchestrator = retryOrchestrator ?? throw new ArgumentNullException(nameof(retryOrchestrator));
            _vBucketKeyMapperFactory = vBucketKeyMapperFactory ?? throw new ArgumentNullException(nameof(vBucketKeyMapperFactory));
            _ketamaKeyMapperFactory = ketamaKeyMapperFactory ?? throw new ArgumentNullException(nameof(ketamaKeyMapperFactory));
            _couchbaseLogger = couchbaseLogger ?? throw new ArgumentNullException(nameof(couchbaseLogger));
            _memcachedLogger = memcachedLogger ?? throw new ArgumentNullException(nameof(memcachedLogger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(IRedactor));
            _bootstrapperFactory = bootstrapperFactory ?? throw new ArgumentNullException(nameof(bootstrapperFactory));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));
            _retryStrategy = retryStrategy ?? throw new ArgumentNullException(nameof(retryStrategy));
            _httpClusterMapFactory = httpClusterMapFactory ?? throw new ArgumentNullException(nameof(httpClusterMapFactory));
            _configPushHandlerFactory = configPushHandlerFactory ?? throw new ArgumentNullException(nameof(configPushHandlerFactory));
        }

        /// <inheritdoc />
        public BucketBase Create(string name, BucketType bucketType, BucketConfig config) =>
            bucketType switch
            {
                BucketType.Couchbase =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger, _redactor, _bootstrapperFactory, _tracer, _operationConfigurator, _retryStrategy, config, _configPushHandlerFactory),
                BucketType.Ephemeral =>
                    new CouchbaseBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _vBucketKeyMapperFactory, _couchbaseLogger, _redactor, _bootstrapperFactory, _tracer, _operationConfigurator, _retryStrategy, config, _configPushHandlerFactory),
                BucketType.Memcached =>
                    new MemcachedBucket(name, _clusterContext, _scopeFactory, _retryOrchestrator, _ketamaKeyMapperFactory, _memcachedLogger, _redactor, _bootstrapperFactory, _tracer, _operationConfigurator, _retryStrategy, _httpClusterMapFactory, config),
                _ => throw new ArgumentOutOfRangeException(nameof(bucketType), bucketType, null)
            };
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
