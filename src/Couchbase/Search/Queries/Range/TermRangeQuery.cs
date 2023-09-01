using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Range
{
    /// <summary>
    /// The term range query finds documents containing a string value in the specified field within the specified range.
    /// Either min or max can be omitted, but not both.
    /// </summary>
    public class TermRangeQuery : SearchQueryBase
    {
        private readonly string _term;
        private string _min;
        private bool _minInclusive = true;
        private string _max;
        private bool _maxInclusive = false;
        private string _field;

        public TermRangeQuery(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("term cannot be null or empty");
            }

            _term = term;
        }

        public TermRangeQuery Min(string min, bool inclusive = true)
        {
            _min = min;
            _minInclusive = inclusive;
            return this;
        }

        public TermRangeQuery Max(string max, bool inclusive = false)
        {
            _max = max;
            _maxInclusive = inclusive;
            return this;
        }

        public TermRangeQuery Field(string field)
        {
            _field = field;
            return this;
        }

        [RequiresUnreferencedCode(SearchClient.SearchRequiresUnreferencedMembersWarning)]
        [RequiresDynamicCode(SearchClient.SearchRequiresDynamicCodeWarning)]
        public override JObject Export()
        {
            if (string.IsNullOrWhiteSpace(_min) && string.IsNullOrWhiteSpace(_max))
            {
                throw new InvalidOperationException("either min or max must be specified");
            }

            var json = base.Export();
            json.Add("term", _term);
            if (!string.IsNullOrWhiteSpace(_min))
            {
                json.Add("min", _min);
                json.Add("inclusive_min", _minInclusive);
            }
            if (!string.IsNullOrWhiteSpace(_max))
            {
                json.Add("max", _max);
                json.Add("inclusive_max", _maxInclusive);
            }
            if (!string.IsNullOrWhiteSpace(_field))
            {
                json.Add("field", _field);
            }

            return json;
        }

        public void Deconstruct(out string term, out string min, out bool minInclusive, out string max, out bool maxInclusive, out string field)
        {
            term = _term;
            min = _min;
            minInclusive = _minInclusive;
            max = _max;
            maxInclusive = _maxInclusive;
            field = _field;
        }

        public ReadOnly AsReadOnly()
        {
            this.Deconstruct(out string term, out string min, out bool minInclusive, out string max, out bool maxInclusive, out string field);
            return new ReadOnly(term, min, minInclusive, max, maxInclusive, field);
        }

        public record ReadOnly(string Term, string Min, bool MinInclusive, string Max, bool MaxInclusive, string Field);
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
