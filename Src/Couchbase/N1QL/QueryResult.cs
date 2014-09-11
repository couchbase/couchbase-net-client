using System;
using System.Collections.Generic;
using Couchbase.N1QL;
using Newtonsoft.Json;

namespace Couchbase.N1QL
{
    /// <summary>
    /// The result of a N1QL query.
    /// </summary>
    /// <typeparam name="T">The Type of each row returned.</typeparam>
    /// <remarks>The dynamic keyword works well for the Type T.</remarks>
    public class QueryResult<T> : IQueryResult<T>
    {
        public QueryResult()
        {
            Rows = new List<T>();
        }
        /// <summary>
        /// The resultset or rows that are returned in a query.
        /// </summary>
        [JsonProperty("resultset")]
        public List<T> Rows { get; set; }

        /// <summary>
        /// Additional information returned by the query.
        /// </summary>
        [JsonProperty("error")]
        public Error Error { get; set; }

        /// <summary>
        /// True if query was successful.
        /// </summary>
        public bool Success { get; internal set; }

        /// <summary>
        /// Optional message returned by query engine or client
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; set; }
    }
}
#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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