#nullable enable
using System;
using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

using Couchbase.Core.Diagnostics.Tracing;

namespace Couchbase.Management.Buckets
{
    public class CreateBucketOptions
    {
        public static ReadOnly DefaultReadOnly => Default.AsReadOnly();
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;
        internal TimeSpan TimeoutValue { get; set; } = ClusterOptions.Default.ManagementTimeout;

        internal IRequestSpan? RequestSpanValue { get; private set; }

        /// <summary>
        /// Allows to pass in a custom CancellationToken from a CancellationTokenSource.
        /// Note that CancellationToken() takes precedence over Timeout(). If both CancellationToken and Timeout are set, the former will be used in the operation.
        /// </summary>
        /// <param name="token">The Token to cancel the operation.</param>
        /// <returns>This class for method chaining.</returns>
        public CreateBucketOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        /// <summary>
        /// Allows to pass in a custom <see cref="IRequestSpan"/>
        /// </summary>
        /// <param name="span">The custom <see cref="IRequestSpan"/> to use for this operation.</param>
        /// <returns>This class for method chaining.</returns>
        public CreateBucketOptions RequestSpan(IRequestSpan span)
        {
            RequestSpanValue = span;
            return this;
        }

        /// <summary>
        /// Allows to set a Timeout for the operation.
        /// Note that CancellationToken() takes precedence over Timeout(). If both CancellationToken and Timeout are set, the former will be used in the operation.
        /// </summary>
        /// <param name="timeout">The duration of the Timeout. Set to 75s by default.</param>
        /// <returns>This class for method chaining.</returns>
        public CreateBucketOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public static CreateBucketOptions Default => new CreateBucketOptions();

        public void Deconstruct(out CancellationToken tokenValue, out TimeSpan timeoutValue)
        {
            tokenValue = TokenValue;
            timeoutValue = TimeoutValue;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out CancellationToken tokenValue, out TimeSpan timeoutValue);
            return new ReadOnly(tokenValue, timeoutValue);
        }

        public record ReadOnly(CancellationToken CancellationToken, TimeSpan Timeout);
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
