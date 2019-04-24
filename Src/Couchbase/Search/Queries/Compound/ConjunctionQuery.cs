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
        private readonly List<IFtsQuery> _queries = new List<IFtsQuery>();

        public ConjunctionQuery(params IFtsQuery[] queries)
        {
           _queries.AddRange(queries);
        }

        /// <summary>
        /// Adds additional <see cref="FtsQueryBase"/> implementations to this <see cref="ConjunctionQuery"/>.
        /// </summary>
        /// <param name="queries">One or more <see cref="FtsQueryBase"/> queries to add.</param>
        /// <returns></returns>
        public ConjunctionQuery And(params IFtsQuery[] queries)
        {
            _queries.AddRange(queries);
            return this;
        }

        public IEnumerator<IFtsQuery> GetEnumerator()
        {
            return _queries.Cast<IFtsQuery>().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

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
