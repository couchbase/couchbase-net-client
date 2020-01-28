using System;
using System.ComponentModel;

namespace Couchbase.Search
{
    /// <summary>
    /// Sets the desired index scan consistency for current N1QL query.
    /// </summary>
    public enum SearchScanConsistency
    {
        /// <summary>
        /// The default which means that the query can return data that is currently indexed
        /// and accessible by the index or the view. The query output can be arbitrarily
        /// out-of-date if there are many pending mutations that have not been indexed by
        /// the index or the view. This consistency level is useful for queries that favor
        /// low latency and do not need precise and most up-to-date information.
        /// </summary>
        [Description("not_bounded")]
        NotBounded,

        /// <summary>
        /// This level provides the strictest consistency level and thus executes with higher
        /// latencies than the other levels. This consistency level requires all mutations, up
        /// to the moment of the query request, to be processed before the query execution can start.
        /// </summary>
        [Description("request_plus")]
        RequestPlus,

        /// <summary>
        /// Do not use; for RYOW use <see cref="ISearchRequest.ConsistentWith"/> and do not specify a <see cref="ScanConsistency"/>.
        /// </summary>
        [Obsolete("Do not use; for RYOW use IQueryRequest.ConsistentWith and do not specify a ScanConsistency.")]
        [Description("at_plus")]
        AtPlus
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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

#endregion
