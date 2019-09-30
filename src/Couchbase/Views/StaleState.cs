namespace Couchbase.Views
{
    /// <summary>
    /// Allow the results from a stale view to be used
    /// </summary>
    public enum StaleState
    {
        None,

        //Force a view update before returning data
        False,
        //Allow stale views
        Ok,
        //Allow stale view, update view after it has been accessed
        UpdateAfter
    }
}
