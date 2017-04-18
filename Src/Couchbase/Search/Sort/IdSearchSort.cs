namespace Couchbase.Search.Sort
{
    /// <summary>
    /// Sorts the search resilts by document ID.
    /// </summary>
    public class IdSearchSort : SearchSortBase
    {
        protected override string By
        {
            get { return "id"; }
        }

        public IdSearchSort(bool decending = false)
        {
            Decending = decending;
        }
    }
}