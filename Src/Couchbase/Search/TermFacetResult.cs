using System.Collections.Generic;

namespace Couchbase.Search
{
    /// <summary>
    /// The result for a <see cref="TermFacet"/>.
    /// </summary>
    public class TermFacetResult : DefaultFacetResult
    {
        /// <summary>
        /// Gets or sets the terms.
        /// </summary>
        /// <value>
        /// The terms.
        /// </value>
        public IReadOnlyCollection<Term> Terms { get; set; }

        /// <summary>
        /// Gets the type of the facet result.
        /// </summary>
        /// <value>
        /// The type of the facet result.
        /// </value>
        public override FacetResultType FacetResultType { get { return FacetResultType.Term;} }
    }
}