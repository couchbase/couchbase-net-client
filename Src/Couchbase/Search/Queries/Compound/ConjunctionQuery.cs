using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Compound
{
    /// <summary>
    /// The conjunction query is a compound query. The result documents must satisfy all of the child queries. It is possible to recursively nest compound queries.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class ConjunctionQuery : FtsQueryBase, IEnumerable<IFtsQuery>
    {
        private readonly List<FtsQueryBase> _queries = new List<FtsQueryBase>();

        public ConjunctionQuery(params FtsQueryBase[] queries)
        {
           _queries.AddRange(queries);
        }

        /// <summary>
        /// Adds additional <see cref="FtsQueryBase"/> implementations to this <see cref="ConjunctionQuery"/>.
        /// </summary>
        /// <param name="queries">One or more <see cref="FtsQueryBase"/> queries to add.</param>
        /// <returns></returns>
        public ConjunctionQuery And(params FtsQueryBase[] queries)
        {
            _queries.AddRange(queries);
            return this;
        }

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public ConjunctionQuery Boost(double boost)
        {
            ((IFtsQuery)this).Boost(boost);
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            if (!_queries.Any())
            {
                throw new InvalidOperationException("A ConjunctionQuery must have a least one child query!");
            }

            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("conjuncts", new JArray(_queries.Select(x => x.Export())))
                )
            ));

            return baseQuery;
        }

        public override JObject Export()
        {
            if (!_queries.Any())
            {
                throw new InvalidOperationException("A ConjunctionQuery must have a least one child query!");
            }

            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("conjuncts", new JArray(_queries.Select(x => x.Export())))
                )
            ));

            return baseQuery;
        }

        public IEnumerator<IFtsQuery> GetEnumerator()
        {
            return _queries.Cast<IFtsQuery>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
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
