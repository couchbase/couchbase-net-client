using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{

    /// <summary>
    /// A docId query is a query that directly matches the documents whose ID have been provided.
    /// It can be combined within a ConjunctionQuery to restrict matches on the set of documents.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class DocIdQuery : FtsQueryBase
    {
        private readonly List<string> _docIds = new List<string>();

        public DocIdQuery(params string[] docIds)
        {
            _docIds.AddRange(docIds);
        }

        /// <summary>
        /// Adds the specified document ids.
        /// </summary>
        /// <param name="docIds">The document ids.</param>
        /// <returns></returns>
        public DocIdQuery Add(params string[] docIds)
        {
            _docIds.AddRange(docIds);
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            var queryJson = base.Export(searchParams);
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("boost", _boost),
                new JProperty("ids", new JArray(_docIds)))));

            return queryJson;
        }

        public override JObject Export()
        {
            var queryJson = base.Export();
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("boost", _boost),
                new JProperty("ids", new JArray(_docIds)))));

            return queryJson;
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
