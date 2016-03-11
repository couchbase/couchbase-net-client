using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// The prefix query finds documents containing terms that start with the provided prefix.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class PrefixQuery : FtsQueryBase
    {
        private readonly string _prefix;
        private string _field;

        public PrefixQuery(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentNullException("prefix");
            }
            _prefix = prefix;
        }

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public PrefixQuery Boost(double boost)
        {
            ((IFtsQuery) this).Boost(boost);
            return this;
        }

        /// <summary>
        /// The field to search against.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public PrefixQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("prefix", _prefix),
                    new JProperty("field", _field))));

            return baseQuery;
        }

        public override JObject Export()
        {
            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("prefix", _prefix),
                    new JProperty("field", _field))));

            return baseQuery;
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
}
