using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Simple
{
    /// <summary>
    ///  A query that analyzes the input text and uses that analyzed text to query the index.
    /// </summary>
    public sealed class MatchQuery : FtsQueryBase
    {
        private readonly string _match;
        private string _analyzer;
        private int _prefixLength;
        private int _fuzziness;
        private string _field;

        public MatchQuery(string match)
        {
            if (string.IsNullOrWhiteSpace(match))
            {
                // ReSharper disable once UseNameofExpression
                throw new ArgumentNullException("match");
            }
            _match = match;
        }

        public MatchQuery Boost(double boost)
        {
            ((IFtsQuery)this).Boost(boost);
            return this;
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

        public override JObject Export(ISearchParams searchParams)
        {
            var queryJson = base.Export(searchParams);
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("match", _match),
                new JProperty("boost", _boost),
                new JProperty("field", _field),
                new JProperty("analyzer", _analyzer),
                new JProperty("prefix_length", _prefixLength),
                new JProperty("fuzziness", _fuzziness))));

            return queryJson;
        }

        public override JObject Export()
        {
            var queryJson = base.Export();
            queryJson.Add(new JProperty("query", new JObject(
                new JProperty("match", _match),
                new JProperty("boost", _boost),
                new JProperty("field", _field),
                new JProperty("analyzer", _analyzer),
                new JProperty("prefix_length", _prefixLength),
                new JProperty("fuzziness", _fuzziness))));

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
