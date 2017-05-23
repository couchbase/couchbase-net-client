namespace Couchbase.Search
{
    /// <summary>
    /// Interface to describe Facet results.
    /// </summary>
    public interface IFacetResult
    {
        /// <summary>
        /// Gets the type of the facet result.
        /// </summary>
        /// <value>
        /// The type of the facet result.
        /// </value>
        FacetResultType FacetResultType { get; }

        /// <summary>
        /// Gets or sets the name of the result.
        /// </summary>
        /// <value>
        /// The name.
        /// </value>
        string Name { get; set; }

        /// <summary>
        /// Gets the field used for the facet.
        /// </summary>
        /// <value>
        /// The field.
        /// </value>
        string Field { get; }

        /// <summary>
        /// Gets the total number of hits for the facet.
        /// </summary>
        /// <value>
        /// The total.
        /// </value>
        long Total { get; }

        /// <summary>
        /// Gets the number of misses for the facet.
        /// </summary>
        /// <value>
        /// The missing.
        /// </value>
        long Missing { get; }

        /// <summary>
        /// Gets the number of others for the facet.
        /// </summary>
        /// <value>
        /// The other.
        /// </value>
        long Other { get; }
    }
}