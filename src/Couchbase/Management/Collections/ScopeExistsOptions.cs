using System;
using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class ScopeExistsOptions
    {
        internal CancellationToken TokenValue { get; set; } = new CancellationTokenSource(ClusterOptions.Default.ManagementTimeout).Token;

        /// <summary>
        /// Allows to pass in a custom CancellationToken from a CancellationTokenSource.
        /// Note that issuing a CancellationToken will replace the Timeout if previously set.
        /// </summary>
        /// <param name="token">The Token to cancel the operation.</param>
        /// <returns></returns>
        public ScopeExistsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        /// <summary>
        /// Allows to set a Timeout for the operation.
        /// Note that issuing a Timeout will replace the CancellationToken if previously set.
        /// </summary>
        /// <param name="timeout">The duration of the Timeout. see <see cref="ClusterOptions"/> for the default value.</param>
        /// <returns></returns>
        public ScopeExistsOptions Timeout(TimeSpan timeout)
        {
            TokenValue = new CancellationTokenSource(timeout).Token;
            return this;
        }

        public static ScopeExistsOptions Default => new ScopeExistsOptions();
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
