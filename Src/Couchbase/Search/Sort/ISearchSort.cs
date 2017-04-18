using Newtonsoft.Json.Linq;

namespace Couchbase.Search.Sort
{
    /// <summary>
    /// Represents the search sort criteria.
    /// </summary>
    public interface ISearchSort
    {
        JObject Export();
    }
}
