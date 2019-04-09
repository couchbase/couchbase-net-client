using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search.Queries.Simple
{
    ///<summary>
    /// A match query searches for terms occurring in the specified positions and offsets.
    /// The input text is analyzed and a phrase query is built with the terms resulting from the analysis.
    /// This depends on term vectors, which are consulted to determine phrase distance.
    /// </summary>
    public sealed class MatchPhraseQuery : FtsQueryBase
    {
        private readonly string _matchPhrase;
        private string _analyzer;
        private string _field;

        public MatchPhraseQuery(string matchPhrase)
        {
            if (string.IsNullOrWhiteSpace(matchPhrase))
            {
                throw new ArgumentNullException("matchPhrase");
            }
            _matchPhrase = matchPhrase;
        }

        public MatchPhraseQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public MatchPhraseQuery Analyzer(string analyzer)
        {
            _analyzer = analyzer;
            return this;
        }

        public override JObject Export()
        {
            var json = base.Export();
            json.Add(new JProperty("match_phrase", _matchPhrase));

            if (!string.IsNullOrEmpty(_field))
            {
                json.Add(new JProperty("field", _field));
            }
            if (!string.IsNullOrEmpty(_analyzer))
            {
                json.Add(new JProperty("analyzer", _analyzer));
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
