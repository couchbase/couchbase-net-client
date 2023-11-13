using System;
using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class CreateScopeOptions
    {
        internal CancellationToken TokenValue { get; set; } = new CancellationTokenSource(ClusterOptions.Default.ManagementTimeout).Token;

        /// <summary>
        /// Allows to pass in a custom CancellationToken from a CancellationTokenSource.
        /// Note that issuing a CancellationToken will replace the Timeout if previously set.
        /// </summary>
        /// <param name="token">The Token to cancel the operation.</param>
        /// <returns></returns>
        public CreateScopeOptions CancellationToken(CancellationToken token)
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
        public CreateScopeOptions Timeout(TimeSpan timeout)
        {
            TokenValue = new CancellationTokenSource(timeout).Token;
            return this;
        }

        public static CreateScopeOptions Default => new CreateScopeOptions();

        public static ReadOnly DefaultReadOnly => CreateScopeOptions.Default.AsReadOnly();

        public void Deconstruct(out CancellationToken tokenValue)
        {
            tokenValue = TokenValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out CancellationToken tokenValue);
            return new ReadOnly(tokenValue);
        }

        public record ReadOnly(CancellationToken CancellationToken);
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
