using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// Regexp query finds documents containing terms that match the specified regular expression.
    /// </summary>
    /// <seealso cref="FtsQueryBase" />
    public class RegexpQuery : FtsQueryBase
    {
        private readonly string _regex;
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
        /// If a field is specified, only terms in that field will be matched. This can also affect the used analyzer if one isn't specified explicitly.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public RegexpQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("regexp", _regex));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }

            return json;
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
