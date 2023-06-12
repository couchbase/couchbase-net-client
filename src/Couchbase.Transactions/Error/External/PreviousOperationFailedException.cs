using System;
using System.Collections.Generic;
using System.Linq;

namespace Couchbase.Transactions.Error.External
{
    /**
     * A previous operation in the application's lambda failed, and so the currently-attempt operation is also not
     * permitted to proceed.
     *
     * This is most likely thrown in one of these two scenarios:
     *
     * 1. The application is performing multiple operations in parallel and one of them has failed.  For performance it is
     *    best to fail all other operations immediately (the transaction is not going to commit anyway), so can get to the
     *    fail and possibly retry point as soon as possible.
     * 2. The application is erroneously catching and not propagating exceptions in the lambda.
     */
    public class PreviousOperationFailedException : Exception
    {
        private IEnumerable<Exception> Causes { get; } = Enumerable.Empty<Exception>();

        /// <inheritdoc />
        public PreviousOperationFailedException()
        {
        }

        /// <inheritdoc />
        public PreviousOperationFailedException(IEnumerable<Exception> causes)
        {
            Causes = causes;
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
