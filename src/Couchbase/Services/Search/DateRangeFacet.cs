using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Couchbase.Services.Search
{
    /// <summary>
    /// A <see cref="ISearchFacet"/> which counts how many documents fall between two <see cref="DateTime"/> values.
    /// </summary>
    public sealed class DateRangeFacet : SearchFacet
    {
        private readonly List<Range<DateTime>> _ranges = new List<Range<DateTime>>();

        public DateRangeFacet()  {  }

        public DateRangeFacet(string name, string field)
        {
            Name = name;
            Field = field;
        }

        public DateRangeFacet(string name, string field, int limit)
        {
            Name = name;
            Field = field;
            Size = limit;
        }

        /// <summary>
        /// Adds a <see cref="Range{DateTime}"/> to the <see cref="ISearchFacet"/>.
        /// </summary>
        /// <param name="startDate">The start date of the range.</param>
        /// <param name="endDate">The end date of the range.</param>
        /// <returns></returns>
        public DateRangeFacet AddRange(DateTime startDate, DateTime endDate)
        {
            AddRange(new Range<DateTime>
            {
                Start = startDate,
                End = endDate
            });
            return this;
        }

        /// <summary>
        /// Adds a <see cref="Range{DateTime}"/> to the <see cref="ISearchFacet"/>.
        /// </summary>
        /// <param name="range">A <see cref="Range{DateTime}"/> for the <see cref="ISearchFacet"/>.</param>
        /// <returns></returns>
        public DateRangeFacet AddRange(Range<DateTime> range)
        {
            _ranges.Add(range);
            return this;
        }

        /// <summary>
        /// Adds a range of <see cref="Range{DateTime}"/>'s to the <see cref="ISearchFacet"/>.
        /// </summary>
        /// <param name="ranges">A range of <see cref="Range{Datetime}"/>'s to add the <see cref="ISearchFacet"/>.</param>
        /// <returns></returns>
        public DateRangeFacet AddRanges(params Range<DateTime>[] ranges)
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
                var range = new JObject(new JProperty("name", r.Name));
                if (r.Start > DateTime.MinValue)
                {
                    range.Add(new JProperty("start", r.Start));
                }
                if (r.End > DateTime.MinValue)
                {
                    range.Add(new JProperty("end", r.End));
                }
                ranges.Add(range);
            }
            return new JProperty(Name, new JObject(
                    new JProperty("field", Field),
                    new JProperty("size", Size),
                    new JProperty("date_ranges", ranges)));
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
