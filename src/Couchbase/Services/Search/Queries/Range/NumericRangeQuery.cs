using System;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search.Queries.Range
{
    /// <summary>
    /// The numeric range query finds documents containing a numeric value in the specified field within the specified range. Either min or max can be omitted, but not both.
    /// </summary>
    /// <seealso cref="FtsQueryBase" />
    public class NumericRangeQuery : FtsQueryBase
    {
        private double? _min;
        private bool _minInclusive = true;
        private double? _max;
        private bool _maxInclusive;
        private string _field;

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

        public override JObject Export()
        {
            if (_min == null && _max == null)
            {
                throw new InvalidOperationException("Either Min or Max can be omitted, but not both.");
            }

            var json = base.Export();
            json.Add(new JProperty("min", _min));
            json.Add(new JProperty("inclusive_min", _minInclusive));
            json.Add(new JProperty("max", _max));
            json.Add(new JProperty("inclusive_max", _maxInclusive));

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
