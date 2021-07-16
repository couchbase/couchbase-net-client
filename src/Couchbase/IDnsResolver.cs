using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase
{
    /// <summary>
    /// Resolves a bootstrap URI to a list of servers using DNS SRV lookup.
    /// </summary>
    public interface IDnsResolver
    {
        Task<IPAddress?> GetIpAddressAsync(string hostName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolve a bootstrap URI to a list of servers using DNS SRV lookup.
        /// </summary>
        /// <param name="bootstrapUri">Bootstrap URI to lookup.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of <seealso cref="HostEndpoint"/> objects, empty if the DNS SRV lookup fails.</returns>
        Task<IEnumerable<HostEndpoint>> GetDnsSrvEntriesAsync(Uri bootstrapUri,
            CancellationToken cancellationToken = default);
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
