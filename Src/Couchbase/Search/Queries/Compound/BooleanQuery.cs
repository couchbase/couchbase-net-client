using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Compound
{
    /// <summary>
    /// A combination of conjunction and disjunction queries.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class BooleanQuery : FtsQueryBase
    {
        private readonly ConjunctionQuery _mustQueries = new ConjunctionQuery();
        private readonly DisjunctionQuery _shouldQueries = new DisjunctionQuery();
        private readonly DisjunctionQuery _mustNotQueries = new DisjunctionQuery();

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public BooleanQuery Boost(double boost)
        {
            ((IFtsQuery) this).Boost(boost);
            return this;
        }

        /// <summary>
        /// Result documents must satisfy these queries.
        /// </summary>
        /// <param name="queries"></param>
        /// <returns></returns>
        public BooleanQuery Must(params FtsQueryBase[] queries)
        {
            _mustQueries.And(queries);
            return this;
        }

        /// <summary>
        /// Result documents should satisfy these queries..
        /// </summary>
        /// <param name="queries">The query.</param>
        /// <returns></returns>
        public BooleanQuery Should(params FtsQueryBase[] queries)
        {
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
            _shouldQueries.Min(min);
            return this;
        }

        /// <summary>
        /// Result documents must not satisfy these queries.
        /// </summary>
        /// <param name="queries">The query.</param>
        /// <returns></returns>
        public BooleanQuery MustNot(params FtsQueryBase[] queries)
        {
            _mustNotQueries.Or(queries);
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            if (!_shouldQueries.Any() && !_mustNotQueries.Any() && !_mustQueries.Any())
            {
                throw new InvalidOperationException("A BooleanQuery must have a least one child query!");
            }

            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("must", new JArray(_mustQueries.Select(x => x.Export()))),
                    new JProperty("must_not", new JArray(_mustNotQueries.Select(x => x.Export()))),
                    new JProperty("should", new JArray(_shouldQueries.Select(x => x.Export())))
                )
            ));

            return baseQuery;
        }

        public override JObject Export()
        {
            if (!_shouldQueries.Any() && !_mustNotQueries.Any() && !_mustQueries.Any())
            {
                throw new InvalidOperationException("A BooleanQuery must have a least one child query!");
            }

            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("must", new JArray(_mustQueries.Select(x => x.Export()))),
                    new JProperty("must_not", new JArray(_mustNotQueries.Select(x => x.Export()))),
                    new JProperty("should", new JArray(_shouldQueries.Select(x => x.Export())))
                )
            ));

            return baseQuery;
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
