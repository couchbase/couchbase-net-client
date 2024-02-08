#if NET5_0_OR_GREATER
#nullable enable
namespace Couchbase.Integrated.Transactions.Error.Attempts
{
    /// <summary>
    /// An exception indicating that a specific entry in an Active Transaction Record was not found when it should have existd.
    /// </summary>
    internal class ActiveTransactionRecordEntryNotFoundException : CouchbaseException
    {
        /// <summary>
        /// Gets the ID of the entry that was supposed to exist.
        /// </summary>
        public string? Id { get; init; } = null;
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
