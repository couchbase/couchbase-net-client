#nullable enable


// ReSharper disable InconsistentNaming

namespace Couchbase.Client.Transactions.Support
{
    /// <summary>
    /// The various states of a transaction attempt.
    /// </summary>
    public enum AttemptStates
    {
        /// <summary>
        /// Nothing has been written yet.
        /// </summary>
        NOTHING_WRITTEN = 0,

        /// <summary>
        /// Mutations are pending.
        /// </summary>
        PENDING = 1,

        /// <summary>
        /// The transaction has been aborted.
        /// </summary>
        ABORTED = 2,

        /// <summary>
        /// The transaction has been completed.
        /// </summary>
        COMMITTED = 3,

        /// <summary>
        /// The transaction has been completed and the metadata cleaned up.
        /// </summary>
        COMPLETED = 4,

        /// <summary>
        /// The transaction was rolled back.
        /// </summary>
        ROLLED_BACK = 5,

        /// <summary>
        /// The transaction is in an unknown state.
        /// </summary>
        UNKNOWN = 6,
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





