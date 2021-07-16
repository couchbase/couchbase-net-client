using System;
using System.Collections.Generic;
using Couchbase.Core.Retry;

namespace Couchbase.Core.Exceptions
{
    /// <summary>
    /// A <see cref="TimeoutException"/> where we are sure there was no side effect on the server.
    /// For example an idempotent operation timeout.
    /// </summary>
    public class UnambiguousTimeoutException : TimeoutException
    {
        public UnambiguousTimeoutException() { }

        public UnambiguousTimeoutException(string message) : base(message) { }

        public UnambiguousTimeoutException(string message, Exception innerException) : base(message, innerException) { }

        public List<RetryReason> RetryReasons { get; } = new List<RetryReason>();

        internal static void ThrowWithRetryReasons(IRequest request, Exception innerException = null)
        {
            var exception = new UnambiguousTimeoutException("The request has timed out.", innerException);
            foreach (var retryReason in request.RetryReasons)
            {
                exception.RetryReasons.Add(retryReason);
            }

            throw exception;
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
