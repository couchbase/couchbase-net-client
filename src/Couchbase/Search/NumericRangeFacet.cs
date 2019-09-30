using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Search
{
    /// <summary>
    /// A <see cref="ISearchFacet"/> which counts how many documents fall between two <see cref="float"/> values.
    /// </summary>
    public sealed class NumericRangeFacet : SearchFacet
    {
        private readonly List<Range<float>> _ranges = new List<Range<float>>();

        public NumericRangeFacet() { }

        public NumericRangeFacet(string name, string field)
        {
            Name = name;
            Field = field;
        }

        public NumericRangeFacet(string name, string field, int limit)
        {
            Name = name;
            Field = field;
            Size = limit;
        }

        /// <summary>
        ///  /// <summary>
        /// Adds a numeric range to the <see cref="ISearchFacet"/>.
        /// </summary>
        /// </summary>
        /// <param name="name">The name of the <see cref="ISearchFacet"/>.</param>
        /// <param name="start">The start of the numeric range."/></param>
        /// <param name="end">The end of the numeric range.</param>
        /// <returns></returns>
        public NumericRangeFacet AddRange(float start, float end)
        {
            AddRange(new Range<float>
            {
                Start = start,
                End = end
            });
            return this;
        }

        /// <summary>
        /// Adds a numeric range to the <see cref="ISearchFacet"/>.
        /// </summary>
        /// <param name="range">A numeric range.</param>
        /// <returns></returns>
        public NumericRangeFacet AddRange(Range<float> range)
        {
            _ranges.Add(range);
            return this;
        }

        /// <summary>
        /// Adds a range of numeric ranges to the <see cref="ISearchFacet"/>
        /// </summary>
        /// <param name="ranges">A range of <see cref="ISearchFacet"/>s.</param>
        /// <returns></returns>
        public NumericRangeFacet AddRanges(params Range<float>[] ranges)
        {
            _ranges.AddRange(ranges);
            return this;
        }

        /// <summary>
        /// Gets the JSON representation of this object.
        /// </summary>
        /// <exception cref="InvalidOperationException">The Name and the Field property must have a value.</exception>
        /// <returns>A <see cref="JObject"/> representing the object's state.</returns>
        public override JProperty ToJson()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                throw new InvalidOperationException("The Name property must have a value.");
            }
            if (string.IsNullOrWhiteSpace(Field))
            {
                throw new InvalidOperationException("The Field property must have a value.");
            }

            var ranges = new JArray();
            foreach (var r in _ranges)
            {
                var range = new JObject();
                range.Add(new JProperty("name", r.Name));
                if (Math.Abs(r.Start) > 0.0F)
                {
                    range.Add(new JProperty("min", r.Start));
                }
                if (Math.Abs(r.End) > 0.0F)
                {
                    range.Add(new JProperty("max", r.End));
                }
                ranges.Add(range);
            }
            return new JProperty(Name, new JObject(
                    new JProperty("field", Field),
                    new JProperty("size", Size),
                    new JProperty("numeric_ranges", ranges)));
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
