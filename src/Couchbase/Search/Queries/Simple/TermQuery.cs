using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    /// A term query is a query that may be "fuzzy" and matches terms within a specified edit distance (Levenshtein distance).
    /// Also, you can optionally specify that the term must have a matching prefix of the specified length.
    /// </summary>
    /// <seealso cref="FtsQueryBase" />
    public sealed class TermQuery : FtsQueryBase
    {
        private readonly string _term;
        private int _fuzziness;
        private int _prefixLength;
        private string _field;

        public TermQuery(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentNullException("term");
            }
            _term = term;
        }

        public TermQuery Fuzziness(int fuzziness)
        {
            if (fuzziness < 0)
            {
                throw new ArgumentOutOfRangeException("fuzziness");
            }
            _fuzziness = fuzziness;
            return this;
        }

        public TermQuery PrefixLength(int prefixLength)
        {
            if (prefixLength < 0)
            {
                throw new ArgumentOutOfRangeException("prefixLength");
            }
            _prefixLength = prefixLength;
            return this;
        }

        /// <summary>
        /// The field to search against.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public TermQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("term", _term));
            json.Add(new JProperty("prefix_length", _prefixLength));
            json.Add(new JProperty("fuzziness", _fuzziness));

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
