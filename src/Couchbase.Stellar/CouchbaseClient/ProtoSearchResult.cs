using System.Collections;
using Newtonsoft.Json;
using Couchbase.Core.Retry;
using Couchbase.Search;

namespace Couchbase.Stellar.CouchbaseClient;

public class ProtoSearchResult : Search.ISearchResult, IDisposable
{
    internal ProtoSearchResult()
    {
        Hits = new List<ISearchQueryRow>();
        Facets = new Dictionary<string, IFacetResult>();
        MetaData = new MetaData();
    }

    public IEnumerator<ISearchQueryRow> GetEnumerator()
    {
        return Hits.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// The rows returned by the search request.
    /// </summary>
    [JsonProperty("hits")]
    public IList<ISearchQueryRow> Hits { get; internal set; }

    /// <summary>
    /// The facets for the result.
    /// </summary>
    [JsonProperty("facets")]
    public IDictionary<string, IFacetResult> Facets { get; internal set; }

    /// <summary>
    /// The search result metadata.
    /// </summary>
    [JsonProperty("metaData")]
    public MetaData MetaData { get; internal set; }

    public RetryReason RetryReason { get; }

    public void Dispose() { }
}