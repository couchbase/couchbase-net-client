using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Configuration.Server;
using Couchbase.Core.Sharding;
using Couchbase.Utils;
using Microsoft.Extensions.Logging;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Default implementation of <see cref="IKetamaKeyMapperFactory"/>.
    /// </summary>
    internal class KetamaKeyMapperFactory : IKetamaKeyMapperFactory
    {
        private readonly ClusterOptions _clusterOptions;
        private readonly ILogger<KetamaKeyMapperFactory> _logger;

        public KetamaKeyMapperFactory(ClusterOptions clusterOptions, ILogger<KetamaKeyMapperFactory> logger)
        {
            _clusterOptions = clusterOptions ?? throw new ArgumentNullException(nameof(clusterOptions));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public KetamaKeyMapper Create(BucketConfig bucketConfig)
        {
            var ipEndPoints = GetEndPoints(bucketConfig);

            return new KetamaKeyMapper(ipEndPoints);
        }

        private IList<HostEndpointWithPort> GetEndPoints(BucketConfig config)
        {
            var endPoints = new List<HostEndpointWithPort>();
            foreach (var node in config.GetNodes().Where(p => p.IsKvNode))
            {
                //log any alternate address mapping
                _logger.LogInformation(node.ToString());

                endPoints.Add(HostEndpointWithPort.Create(node, _clusterOptions));
            }

            return endPoints;
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
