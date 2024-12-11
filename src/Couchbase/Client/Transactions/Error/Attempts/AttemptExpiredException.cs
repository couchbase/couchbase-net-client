#nullable enable
using System;

namespace Couchbase.Client.Transactions.Error.Attempts
{
    /// <summary>
    /// Indicates an attempt exceeded the allotted time.
    /// </summary>
    internal class AttemptExpiredException : AttemptException
    {
        /// <summary>
        /// Initializes a new instance of the AttemptExpiredException class.
        /// </summary>
        /// <param name="ctx">The AttemptContext.</param>
        /// <param name="msg">The message.</param>
        public AttemptExpiredException(AttemptContext ctx, string? msg = null)
            : base(ctx, msg ?? "Attempt Expired")
        {
        }

        /// <summary>
        /// Initializes a new instance of the AttemptExpiredException class.
        /// </summary>
        /// <param name="ctx">The AttemptContext.</param>
        /// <param name="msg">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public AttemptExpiredException(AttemptContext ctx, string msg, Exception innerException)
            : base(ctx, msg, innerException)
        {
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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





