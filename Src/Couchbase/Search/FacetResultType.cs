namespace Couchbase.Search
{
    /// <summary>
    /// The type of facet result.
    /// </summary>
    public enum FacetResultType
    {
        Unknown,
        Term,
        NumericRange,
        DateRange
    }
}