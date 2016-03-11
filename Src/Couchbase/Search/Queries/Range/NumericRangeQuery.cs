using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Queries.Range
{
    /// <summary>
    /// The numeric range query finds documents containing a numeric value in the specified field within the specified range. Either min or max can be omitted, but not both.
    /// </summary>
    /// <seealso cref="Couchbase.Search.Queries.FtsQueryBase" />
    public class NumericRangeQuery : FtsQueryBase
    {
        private double? _min;
        private bool _minInclusive = true;
        private double? _max;
        private bool _maxInclusive;
        private string _field;

        /// <summary>
        /// Used to increase the relative weight of a clause (with a boost greater than 1) or decrease the relative weight (with a boost between 0 and 1).
        /// </summary>
        /// <param name="boost"></param>
        /// <returns></returns>
        public NumericRangeQuery Boost(double boost)
        {
            ((IFtsQuery)this).Boost(boost);
            return this;
        }

        /// <summary>
        /// The lower end of the range, inclusive by default.
        /// </summary>
        /// <param name="min">The minimum.</param>
        /// <param name="inclusive">if set to <c>true</c> [inclusive].</param>
        /// <returns></returns>
        public NumericRangeQuery Min(double min, bool inclusive = true)
        {
            _min = min;
            _minInclusive = inclusive;
            return this;
        }

        /// <summary>
        /// The higher end of the range, exclusive by default.
        /// </summary>
        /// <param name="max">The maximum.</param>
        /// <param name="inclusive">if set to <c>true</c> [inclusive].</param>
        /// <returns></returns>
        public NumericRangeQuery Max(double max, bool inclusive = false)
        {
            _max = max;
            _maxInclusive = inclusive;
            return this;
        }

        /// <summary>
        /// If a field is specified, only terms in that field will be matched. This can also affect the used analyzer if one isn't specified explicitly.
        /// </summary>
        /// <param name="field">The field.</param>
        /// <returns></returns>
        public NumericRangeQuery Field(string field)
        {
            _field = field;
            return this;
        }

        public override JObject Export(ISearchParams searchParams)
        {
            if (_min == null && _max == null)
            {
                throw new InvalidOperationException("Either Min or Max can be omitted, but not both.");
            }

            var baseQuery = base.Export(searchParams);
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("field", _field),
                    new JProperty("min", _min),
                    new JProperty("inclusive_min", _minInclusive),
                    new JProperty("max", _max),
                    new JProperty("inclusive_max", _maxInclusive))));

            return baseQuery;
        }

        public override JObject Export()
        {
            var baseQuery = base.Export();
            baseQuery.Add(new JProperty("query",
                new JObject(
                    new JProperty("boost", _boost),
                    new JProperty("field", _field),
                    new JProperty("min", _min),
                    new JProperty("inclusive_min", _minInclusive),
                    new JProperty("max", _max),
                    new JProperty("inclusive_max", _maxInclusive))));

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
