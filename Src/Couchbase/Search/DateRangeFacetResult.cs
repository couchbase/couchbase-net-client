using System.Collections.Generic;

namespace Couchbase.Search
{
    /// <summary>
    /// The result for a <see cref="DateRangeFacet"/>.
    /// </summary>
    public class DateRangeFacetResult : DefaultFacetResult
    {
        public DateRangeFacetResult()
        {
            DateRanges = new List<DateRange>();
        }

        /// <summary>
        /// Gets or sets the date ranges.
        /// </summary>
        /// <value>
        /// The date ranges.
        /// </value>
        public IReadOnlyCollection<DateRange> DateRanges { get; set; }

        /// <summary>
        /// Gets the type of the facet result.
        /// </summary>
        /// <value>
        /// The type of the facet result.
        /// </value>
        public override FacetResultType FacetResultType { get { return FacetResultType.DateRange; } }
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
