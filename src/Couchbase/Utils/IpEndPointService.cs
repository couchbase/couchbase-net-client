using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Default implementation of <see cref="IIpEndPointService"/>.
    /// </summary>
    internal class IpEndPointService : IIpEndPointService
    {
        private readonly IDnsResolver _dnsResolver;

        public IpEndPointService(IDnsResolver dnsResolver)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (dnsResolver == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(dnsResolver));
            }

            _dnsResolver = dnsResolver;
        }

        public async ValueTask<IPEndPoint?> GetIpEndPointAsync(string hostNameOrIpAddress, int port, CancellationToken cancellationToken = default)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (hostNameOrIpAddress == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(hostNameOrIpAddress));
            }

            if (!IPAddress.TryParse(hostNameOrIpAddress, out IPAddress? ipAddress))
            {
                ipAddress = await _dnsResolver.GetIpAddressAsync(hostNameOrIpAddress, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (ipAddress == null)
            {
                ThrowHelper.ThrowInvalidArgumentException($"Cannot resolve DNS for {hostNameOrIpAddress}.");
            }

            return new IPEndPoint(ipAddress, port);
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
