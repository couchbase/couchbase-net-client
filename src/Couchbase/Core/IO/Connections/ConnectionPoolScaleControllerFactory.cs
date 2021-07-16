using System;
using Couchbase.Core.Logging;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Default implementation of <see cref="IConnectionPoolScaleControllerFactory"/>.
    /// </summary>
    internal class ConnectionPoolScaleControllerFactory : IConnectionPoolScaleControllerFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly IRedactor _redactor;
        private readonly ILogger<DefaultConnectionPoolScaleController> _logger;

        public ConnectionPoolScaleControllerFactory(ClusterOptions clusterOptions, IRedactor redactor,
            ILogger<DefaultConnectionPoolScaleController> logger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public IConnectionPoolScaleController Create()
        {
            return new DefaultConnectionPoolScaleController(_redactor, _logger)
            {
                IdleConnectionTimeout = _clusterOptions.IdleKvConnectionTimeout
            };
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
