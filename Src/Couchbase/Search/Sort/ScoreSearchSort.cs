namespace Couchbase.Search.Sort
{
    /// <summary>
    /// Sorts the search results by hit score.
    /// </summary>
    public class ScoreSearchSort : SearchSortBase
    {
        protected override string By
        {
            get { return "score"; }
        }

        public ScoreSearchSort(bool decending = false)
        {
            Decending = decending;
        }
    }
}