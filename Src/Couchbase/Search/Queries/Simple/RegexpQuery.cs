using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// Regexp query finds documents containing terms that match the specified regular expression.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class RegexpQuery : FtsQueryBase
    {
        private string _regex;
        private string _field;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexpQuery"/> class.
        /// </summary>
        /// <param name="regex">The regexp to be analyzed and used against. The regexp string is required.</param>
        /// <exception cref="System.ArgumentNullException">regex</exception>
        public RegexpQuery(string regex)
        {
            if (string.IsNullOrWhiteSpace(regex))
            {
                throw new ArgumentNullException("regex");
            }
            _regex = regex;
        }

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public RegexpQuery Boost(double boost)
        {
            ((IFtsQuery)this).Boost(boost);
            return this;
        }

        /// <summary>
        /// If a field is specified, only terms in that field will be matched. This can also affect the used analyzer if one isn't specified explicitly.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public RegexpQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            var queryJson = base.Export(searchParams);
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("boost", _boost),
                new JProperty("field", _field),
                new JProperty("regexp", _regex))));

            return queryJson;
        }

        public override JObject Export()
        {
            var queryJson = base.Export();
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("boost", _boost),
                new JProperty("field", _field),
                new JProperty("regexp", _regex))));

            return queryJson;
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
