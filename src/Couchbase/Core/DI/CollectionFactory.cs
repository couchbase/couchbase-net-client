using System;
using Couchbase.Core.Diagnostics.Tracing;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Transcoders;
using Couchbase.Core.Logging;
using Couchbase.KeyValue;
using Couchbase.Management.Query;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="ICollectionFactory"/>.
    /// </summary>
    internal class CollectionFactory : ICollectionFactory
    {
        private readonly IOperationConfigurator _operationConfigurator;
        private readonly ILogger<CouchbaseCollection> _logger;
        private readonly ILogger<GetResult> _getLogger;
        private readonly IRedactor _redactor;
        private readonly IRequestTracer _tracer;
        private readonly ICollectionQueryIndexManagerFactory _queryIndexManagerFactory;

        public CollectionFactory(IOperationConfigurator operationConfigurator, ILogger<CouchbaseCollection> logger,
            ILogger<GetResult> getLogger, IRedactor redactor, IRequestTracer tracer, ICollectionQueryIndexManagerFactory queryIndexManagerFactory)
        {
            _operationConfigurator = operationConfigurator ?? throw new ArgumentNullException(nameof(operationConfigurator));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _getLogger = getLogger ?? throw new ArgumentNullException(nameof(getLogger));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _queryIndexManagerFactory = queryIndexManagerFactory ?? throw new ArgumentNullException(nameof(queryIndexManagerFactory));
        }

        /// <inheritdoc />
        public ICouchbaseCollection Create(BucketBase bucket, IScope scope, string name) =>
            new CouchbaseCollection(bucket, _operationConfigurator, _logger, _getLogger, _redactor, name, scope, _tracer, _queryIndexManagerFactory);
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
