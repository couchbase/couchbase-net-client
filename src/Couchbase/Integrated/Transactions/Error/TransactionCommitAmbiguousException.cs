#nullable enable
using System;

namespace Couchbase.Integrated.Transactions.Error
{
    /// <summary>
    /// Indicates a transaction failed in a way that the client cannot know if it successfully committed.
    /// </summary>
    internal class TransactionCommitAmbiguousException : TransactionFailedException
    {
        /// <inheritdoc />
        public TransactionCommitAmbiguousException(string message, Exception innerException, TransactionResult? result)
            : base(message, innerException, result)
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





