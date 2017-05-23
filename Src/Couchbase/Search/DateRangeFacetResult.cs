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