using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Range
{
    /// <summary>
    /// The date range query finds documents containing a date value in the specified field within the specified range.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class DateRangeQuery : FtsQueryBase
    {
        private DateTime? _startTime;
        private bool _inclusiveStart = true;
        private DateTime? _endTime;
        private bool _inclusiveEnd;
        private string _parserName;
        private string _field;

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public DateRangeQuery Boost(double boost)
        {
            ((IFtsQuery)this).Boost(boost);
            return this;
        }

        /// <summary>
        /// The start date of the range.
        /// </summary>
        /// <param name="startTime">The start time.</param>
        /// <param name="inclusive">if set to <c>true</c> [inclusive].</param>
        /// <returns></returns>
        public DateRangeQuery Start(DateTime startTime, bool inclusive=true)
        {
            _startTime = startTime;
            _inclusiveStart = inclusive;
            return this;
        }

        /// <summary>
        /// The end date of the range
        /// </summary>
        /// <param name="endTime">The end time.</param>
        /// <param name="inclusive">if set to <c>true</c> [inclusive].</param>
        /// <returns></returns>
        public DateRangeQuery End(DateTime endTime, bool inclusive= false)
        {
            _endTime = endTime;
            _inclusiveEnd = inclusive;
            return this;
        }

        /// <summary>
        /// If a field is specified, only terms in that field will be matched. This can also affect the used analyzer if one isn't specified explicitly.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public DateRangeQuery Field(string field)
        {
            _field = field;
            return this;
        }

        /// <summary>
        /// The name of the parser to use.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public DateRangeQuery Parser(string name)
        {
            _parserName = name;
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            if (_startTime == null && _endTime == null)
            {
                throw new InvalidOperationException("Either Start or End can be omitted, but not both.");
            }

            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("field", _field),
                    new JProperty("start", _startTime),
                    new JProperty("inclusive_start", _inclusiveStart),
                    new JProperty("end", _endTime),
                    new JProperty("inclusive_end", _inclusiveEnd),
                    new JProperty("datetime_parser", _parserName))));

            return baseQuery;
        }

        public override JObject Export()
        {
            if (_startTime == null && _endTime == null)
            {
                throw new InvalidOperationException("Either Start or End can be omitted, but not both.");
            }

            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("field", _field),
                    new JProperty("start", _startTime),
                    new JProperty("inclusive_start", _inclusiveStart),
                    new JProperty("end", _endTime),
                    new JProperty("inclusive_end", _inclusiveEnd),
                    new JProperty("datetime_parser", _parserName))));

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
