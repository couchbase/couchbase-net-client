using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// The query string query allows humans to describe complex queries using a simple syntax.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class StringQuery : FtsQueryBase
    {
        private readonly string _query;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringQuery"/> class.
        /// </summary>
        /// <param name="query">The query string to be analyzed and used against. The query string is required.</param>
        /// <exception cref="System.ArgumentNullException">query</exception>
        public StringQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentNullException("query");
            }
            _query = query;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("query", _query));

            return json;
        }
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
