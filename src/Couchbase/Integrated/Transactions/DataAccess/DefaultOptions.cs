#if NET5_0_OR_GREATER
#nullable enable
using System;
using Couchbase.Core.Retry;
using Couchbase.KeyValue;

namespace Couchbase.Integrated.Transactions.DataAccess
{
    internal static class DefaultOptions
    {
        public static IRetryStrategy RetryStrategy = new BestEffortRetryStrategy();

        public static LookupInOptions Defaults(this LookupInOptions opts, TimeSpan? timeout)
        {
            opts = opts.RetryStrategy(RetryStrategy);
            if (timeout.HasValue)
            {
                opts = opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static MutateInOptions Defaults(this MutateInOptions opts, DurabilityLevel? durability, TimeSpan? timeout)
        {
            opts = new MutateInOptions().RetryStrategy(RetryStrategy);
            if (durability.HasValue)
            {
                opts = opts.Durability(durability.Value);
            }

            if (timeout.HasValue)
            {
                opts = opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static InsertOptions Defaults(this InsertOptions opts, DurabilityLevel? durability, TimeSpan? timeout)
        {
            opts = new InsertOptions().RetryStrategy(RetryStrategy);
            if (durability.HasValue)
            {
                opts = opts.Durability(durability.Value);
            }

            if (timeout.HasValue)
            {
                opts = opts.Timeout(timeout.Value);
            }

            return opts;
        }

        public static RemoveOptions Defaults(this RemoveOptions opts, DurabilityLevel? durability, TimeSpan? timeout)
        {
            opts = new RemoveOptions().RetryStrategy(RetryStrategy);
            if (durability.HasValue)
            {
                opts = opts.Durability(durability.Value);
            }

            if (timeout.HasValue)
            {
                opts = opts.Timeout(timeout.Value);
            }

            return opts;
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
#endif
