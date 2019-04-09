using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search.Queries.Simple
{
    /// <summary>
    /// A wildcard query is a query in which term the character * will match 0..n occurrences of any characters and ? will match 1 occurrence of any character.
    /// </summary>
    /// <seealso cref="FtsQueryBase" />
    public class WildcardQuery : FtsQueryBase
    {
        private readonly string _wildCard;
        private string _field;

        public WildcardQuery(string wildcard)
        {
            _wildCard = wildcard;
        }

        /// <summary>
        /// The field for the match.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public WildcardQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("wildcard", _wildCard));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }

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
