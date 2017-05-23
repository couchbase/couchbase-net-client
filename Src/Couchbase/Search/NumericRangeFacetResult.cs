using System.Collections.Generic;

namespace Couchbase.Search
{
    /// <summary>
    /// The result for a <see cref="NumericRangeFacet"/>.
    /// </summary>
    public class NumericRangeFacetResult : DefaultFacetResult
    {
        public NumericRangeFacetResult()
        {
            NumericRanges = new List<NumericRange>();
        }

        /// <summary>
        /// Gets or sets the numeric ranges.
        /// </summary>
        /// <value>
        /// The numeric ranges.
        /// </value>
        public IReadOnlyCollection<NumericRange> NumericRanges { get; set; }

        /// <summary>
        /// Gets the type of the facet result.
        /// </summary>
        /// <value>
        /// The type of the facet result.
        /// </value>
        public override FacetResultType FacetResultType { get { return FacetResultType.NumericRange; } }
    }
}