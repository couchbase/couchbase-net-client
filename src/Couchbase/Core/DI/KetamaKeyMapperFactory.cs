using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
        private readonly IIpEndPointService _ipEndPointService;
        private readonly ILogger<IIpEndPointService> _logger;

        public KetamaKeyMapperFactory(IIpEndPointService ipEndPointService, ILogger<IIpEndPointService> logger)
        {
            _ipEndPointService = ipEndPointService ?? throw new ArgumentNullException(nameof(ipEndPointService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<KetamaKeyMapper> CreateAsync(BucketConfig bucketConfig, CancellationToken cancellationToken = default)
        {
            var ipEndPoints = await GetIpEndPointsAsync(bucketConfig, cancellationToken).ConfigureAwait(false);

            return new KetamaKeyMapper(ipEndPoints);
        }

        private async Task<IList<IPEndPoint>> GetIpEndPointsAsync(BucketConfig config, CancellationToken cancellationToken)
        {
            var ipEndPoints = new List<IPEndPoint>();
            foreach (var node in config.GetNodes().Where(p => p.IsKvNode))
            {
                //log any alternate address mapping
                _logger.LogInformation(node.ToString());

                var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(node, cancellationToken).ConfigureAwait(false);
                if (ipEndPoint == null)
                {
                    throw new InvalidOperationException("IP endpoint lookup failed.");
                }

                ipEndPoints.Add(ipEndPoint);
            }

            return ipEndPoints;
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
