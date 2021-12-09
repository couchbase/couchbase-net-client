using System;
using Couchbase.Core.Compatibility;
using Couchbase.Utils;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    ///  A query that analyzes the input text and uses that analyzed text to query the index.
    /// </summary>
    public sealed class MatchQuery : SearchQueryBase
    {
        private readonly string _match;
        private string _analyzer;
        private int _prefixLength;
        private int _fuzziness;
        private string _field;
        private MatchOperator? _matchOperator;

        /// <summary>
        /// Specifies how the individual match terms should be logically concatenated.
        /// </summary>
        /// <param name="matchOperator">The <see cref="MatchOperator"/> used to match terms.</param>
        /// <returns>A <see cref="MatchQuery"/> object for chaining.</returns>
        [InterfaceStability(Level.Uncommitted)]
        public MatchQuery MatchOperator(MatchOperator matchOperator)
        {
            _matchOperator = matchOperator;
            return this;
        }

        public MatchQuery(string match)
        {
            if (string.IsNullOrWhiteSpace(match))
            {
                // ReSharper disable once UseNameofExpression
                throw new ArgumentNullException("match");
            }
            _match = match;
        }

        public MatchQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public MatchQuery Analyzer(string analyzer)
        {
            _analyzer = analyzer;
            return this;
        }

        public MatchQuery Fuzziness(int fuzziness)
        {
            if (fuzziness < 0)
            {
                // ReSharper disable once UseNameofExpression
                throw new ArgumentOutOfRangeException("fuzziness");
            }
            _fuzziness = fuzziness;
            return this;
        }

        public MatchQuery PrefixLength(int prefixLength)
        {
            if (prefixLength < 0)
            {
                // ReSharper disable once UseNameofExpression
                throw new ArgumentOutOfRangeException("prefixLength");
            }
            _prefixLength = prefixLength;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("match", _match));
            json.Add(new JProperty("prefix_length", _prefixLength));
            json.Add(new JProperty("fuzziness", _fuzziness));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }
            if (!string.IsNullOrEmpty(_analyzer))
            {
                json.Add(new JProperty("analyzer", _analyzer));
            }

            if (_matchOperator.HasValue)
            {
                json.Add(new JProperty("operator", _matchOperator.GetDescription()));
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
