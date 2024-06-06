#nullable enable
using System;

namespace Couchbase.Integrated.Transactions.Error.Attempts
{
    /// <summary>
    /// Indicates an error during an individual transaction attempt.
    /// </summary>
    internal class AttemptException : CouchbaseException
    {
        private AttemptContext _ctx;

        /// <summary>
        /// Initializes a new instance of the AttemptException class.
        /// </summary>
        /// <param name="ctx">The Attempt Context.</param>
        /// <param name="msg">The message.</param>
        public AttemptException(AttemptContext ctx, string msg)
            : base(msg)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Initializes a new instance of the AttemptException class.
        /// </summary>
        /// <param name="ctx">The AttemptContext.</param>
        /// <param name="msg">The message.</param>
        /// <param name="innerException">The inner exception.</param>
        public AttemptException(AttemptContext ctx, string msg, Exception innerException)
            : base(msg, innerException)
        {
            _ctx = ctx;
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





