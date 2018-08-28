using System;

namespace Couchbase.Search
{
    public class SearchMetrics
    {
        /// <summary>
        /// The number of shards (pindex) of the FTS index that were successfully queried, returning hits.
        /// </summary>
        public long SuccessCount { get; set; }

        /// <summary>
        /// The count of errors.
        /// </summary>
        public long ErrorCount { get; set; }

        /// <summary>
        /// Gets the total count.
        /// </summary>
        public long TotalCount { get; set; }

        /// <summary>
        /// Total time taken for the results.
        /// </summary>
        public TimeSpan Took { get; set; }

        /// <summary>
        /// Total hits returned by the results.
        /// </summary>
        public long TotalHits { get; set; }

        /// <summary>
        /// The maximum score within the results.
        /// </summary>
        public double MaxScore { get; set; }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2015 Couchbase, Inc.
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
