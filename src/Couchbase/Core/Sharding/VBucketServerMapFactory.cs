using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core.Sharding
{
    /// <summary>
    /// Default implementation of <see cref="IVBucketServerMapFactory"/>.
    /// </summary>
    internal class VBucketServerMapFactory : IVBucketServerMapFactory
    {
        private readonly IIpEndPointService _ipEndPointService;

        public VBucketServerMapFactory(IIpEndPointService ipEndPointService)
        {
            if (ipEndPointService == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(ipEndPointService));
            }

            _ipEndPointService = ipEndPointService;
        }

        /// <inheritdoc />
        public async Task<VBucketServerMap> CreateAsync(VBucketServerMapDto serverMapDto,
            CancellationToken cancellationToken = default) =>
            new VBucketServerMap(serverMapDto,
                await GetIpEndPointsAsync(serverMapDto.ServerList, cancellationToken).ToListAsync(cancellationToken)
                    .ConfigureAwait(false));

        private async IAsyncEnumerable<IPEndPoint> GetIpEndPointsAsync(IEnumerable<string> serverList,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var server in serverList)
            {
                var (hostName, port) = HostEndpoint.Parse(server);
                if (port == null)
                {
                    // Should not happen with data from BucketConfig
                    ThrowHelper.ThrowInvalidOperationException("Server list is missing port numbers.");
                }

                var ipEndPoint = await _ipEndPointService.GetIpEndPointAsync(hostName, port.Value, cancellationToken)
                    .ConfigureAwait(false);
                if (ipEndPoint == null)
                {
                    ThrowHelper.ThrowArgumentException($"Unable to resolve '{server}'.", nameof(serverList));
                }

                yield return ipEndPoint;
            }
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
