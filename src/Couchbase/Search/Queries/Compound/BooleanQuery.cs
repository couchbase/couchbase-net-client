using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json.Linq;
#nullable enable
namespace Couchbase.Search.Queries.Compound
{
    /// <summary>
    /// A combination of conjunction and disjunction queries.
    /// </summary>
    /// <seealso cref="SearchQueryBase" />
    public class BooleanQuery : SearchQueryBase
    {
        private ConjunctionQuery? _mustQueries;
        private DisjunctionQuery? _shouldQueries;
        private DisjunctionQuery? _mustNotQueries;

        /// <summary>
        /// Result documents must satisfy these queries.
        /// </summary>
        /// <param name="queries"></param>
        /// <returns></returns>
        public BooleanQuery Must(params ISearchQuery[] queries)
        {
            _mustQueries ??= new();
            _mustQueries.And(queries);
            return this;
        }

        /// <summary>
        /// Result documents should satisfy these queries..
        /// </summary>
        /// <param name="queries">The query.</param>
        /// <returns></returns>
        public BooleanQuery Should(params ISearchQuery[] queries)
        {
            _shouldQueries ??= new();
            _shouldQueries.Or(queries);
            return this;
        }

        /// <summary>
        /// If a hit satisfies at least min queries in the should be boosted by this amount.
        /// </summary>
        /// <param name="min">The minimum to boost by - the default is 1.</param>
        /// <returns></returns>
        public BooleanQuery ShouldMin(int min)
        {
            _shouldQueries ??= new();
            _shouldQueries.Min(min);
            return this;
        }

        /// <summary>
        /// Result documents must not satisfy these queries.
        /// </summary>
        /// <param name="queries">The query.</param>
        /// <returns></returns>
        public BooleanQuery MustNot(params ISearchQuery[] queries)
        {
            _mustNotQueries ??= new();
            _mustNotQueries.Or(queries);
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            if (_shouldQueries is null && _mustQueries is null && _mustNotQueries is null)
            {
                throw new InvalidOperationException("A BooleanQuery must have a least one child query!");
            }

            var json = base.Export();
            if (_mustQueries?.Any() == true)
            {
                json.Add(new JProperty("must", _mustQueries.Export()));
            }
            if (_mustNotQueries?.Any() == true)
            {
                json.Add(new JProperty("must_not", _mustNotQueries.Export()));
            }
            if (_shouldQueries?.Any() == true)
            {
                json.Add(new JProperty("should", _shouldQueries.Export()));
            }

            return json;
        }

        public void Deconstruct(out ConjunctionQuery? mustQueries, out DisjunctionQuery? shouldQueries, out DisjunctionQuery? mustNotQueries)
        {
            mustQueries = _mustQueries;
            shouldQueries = _shouldQueries;
            mustNotQueries = _mustNotQueries;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out ConjunctionQuery? mustQueries, out DisjunctionQuery? shouldQueries, out DisjunctionQuery? mustNotQueries);
            return new ReadOnly(mustQueries, shouldQueries, mustNotQueries);
        }

        public record ReadOnly(ConjunctionQuery? MustQueries, DisjunctionQuery? ShouldQueries, DisjunctionQuery? MustNotQueries);
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
