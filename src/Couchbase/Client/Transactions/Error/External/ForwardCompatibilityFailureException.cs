#nullable enable
using System;

namespace Couchbase.Client.Transactions.Error.External
{
    /// <summary>
    /// Indicates that this version of the transactions protocol encountered a document with metadata from a later
    /// version which it cannot safely interact with.
    /// </summary>
    internal class ForwardCompatibilityFailureException : CouchbaseException
    {
        /// <inheritdoc />
        public ForwardCompatibilityFailureException() : base()
        {
        }

        /// <inheritdoc />
        public ForwardCompatibilityFailureException(string message) : base(message)
        {
        }

        /// <inheritdoc />
        public ForwardCompatibilityFailureException(string message, Exception innerException) : base(message, innerException)
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





