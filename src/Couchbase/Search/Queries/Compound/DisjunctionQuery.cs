using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Couchbase.Core.Exceptions;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Compound
{
    /// <summary>
    /// The disjunction query is a compound query. The result documents must satisfy a configurable min number of child queries. By default this min is set to 1.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public class DisjunctionQuery : SearchQueryBase, IEnumerable<ISearchQuery>
    {
        private int? _min;
        private readonly List<ISearchQuery> _queries;

        public DisjunctionQuery(params ISearchQuery[] queries)
        {
            _queries = new List<ISearchQuery>(queries);
        }

        /// <summary>
        /// Adds additional <see cref="SearchQueryBase"/> implementations to this <see cref="ConjunctionQuery"/>.
        /// </summary>
        /// <param name="queries">One or more <see cref="SearchQueryBase"/> queries to add.</param>
        /// <returns></returns>
        public DisjunctionQuery Or(params ISearchQuery[] queries)
        {
            _queries.AddRange(queries);
            return this;
        }

        /// <summary>
        /// The minimum number of child queries that must be satisfied for the disjunction query.
        /// </summary>
        /// <param name="min">The minimum.</param>
        /// <returns></returns>
        public DisjunctionQuery Min(int min)
        {
            if (min < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(min));
            }
            _min = min;
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
                throw new InvalidArgumentException("A DisjunctionQuery must have a least one child query!");
            }
            if (_min.HasValue && _min > _queries.Count)
            {
                throw new InvalidArgumentException($"The Disjunction query has fewer children than the configured minimum of {_min}");
            }
            var json = base.Export();
            if (_min.HasValue) json.Add(new JProperty("min", _min));
            json.Add(new JProperty("disjuncts", new JArray(_queries.Select(x => x.Export()))));

            return json;
        }

        public void Deconstruct(out int? min, out IReadOnlyList<ISearchQuery> queries)
        {
            min = _min;
            queries = _queries;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out int? min, out IReadOnlyList<ISearchQuery> queries);
            return new ReadOnly(min, queries);
        }

        public record ReadOnly(int? Min, IReadOnlyList<ISearchQuery> Queries);
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
