using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// A boolean field query matches documents which have a boolean field which corresponds to the requested boolean value.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class BooleanFieldQuery : FtsQueryBase
    {
        private bool _fieldMatch;
        private string _field;

        public BooleanFieldQuery(bool fieldMatch)
        {
            _fieldMatch = fieldMatch;
        }

        /// <summary>
        /// The field for the match.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public BooleanFieldQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("bool", _fieldMatch),
                    new JProperty("boost", _boost),
                    new JProperty("field", _field))));

            return baseQuery;
        }

        public override JObject Export()
        {
            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("bool", _fieldMatch),
                    new JProperty("boost", _boost),
                    new JProperty("field", _field))));

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
