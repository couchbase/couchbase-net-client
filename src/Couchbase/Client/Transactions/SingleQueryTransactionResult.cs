#nullable enable
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Compatibility;
using Couchbase.Query;

namespace Couchbase.Client.Transactions
{
    /// <summary>
    /// The transaction result from a single query transaction.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public class SingleQueryTransactionResult<T>
    {
        /// <summary>
        /// Gets the query result, if any.
        /// </summary>
        public IQueryResult<T>? QueryResult { get; internal set; } = null;

        /// <summary>
        /// Gets the logs from the transaction.
        /// </summary>
        public IEnumerable<string> Logs { get; internal set; } = Enumerable.Empty<string>();

        /// <summary>
        /// Gets a value indicating whether the transaction completed to the point of unstaging its results, meaning it finished successfully.
        /// </summary>
        public bool UnstagingComplete { get; internal set; }
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





