using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Compound
{
    /// <summary>
    /// The conjunction query is a compound query. The result documents must satisfy all of the child queries. It is possible to recursively nest compound queries.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public class ConjunctionQuery : SearchQueryBase, IEnumerable<ISearchQuery>
    {
        private readonly List<ISearchQuery> _queries = new List<ISearchQuery>();

        public ConjunctionQuery(params ISearchQuery[] queries)
        {
           _queries.AddRange(queries);
        }

        /// <summary>
        /// Adds additional <see cref="SearchQueryBase"/> implementations to this <see cref="ConjunctionQuery"/>.
        /// </summary>
        /// <param name="queries">One or more <see cref="SearchQueryBase"/> queries to add.</param>
        /// <returns></returns>
        public ConjunctionQuery And(params ISearchQuery[] queries)
        {
            _queries.AddRange(queries);
            return this;
        }

        public IEnumerator<ISearchQuery> GetEnumerator()
        {
            return _queries.Cast<ISearchQuery>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            if (!_queries.Any())
            {
                throw new InvalidOperationException("A ConjunctionQuery must have a least one child query!");
            }

            var json = base.Export();
            json.Add(new JProperty("conjuncts", new JArray(_queries.Select(x => x.Export()))));

            return json;
        }

        public void Deconstruct(out IReadOnlyList<ISearchQuery> queries)
        {
            queries = _queries;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out IReadOnlyList<ISearchQuery> queries);
            return new ReadOnly(queries);
        }

        public record ReadOnly(IReadOnlyList<ISearchQuery> Queries);
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
