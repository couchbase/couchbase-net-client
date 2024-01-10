#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections;
using System.Collections.Generic;
using Couchbase.Core.Retry;
using Couchbase.Search;
using Newtonsoft.Json;

namespace Couchbase.Stellar.Search;

public class StellarSearchResult : ISearchResult, IDisposable
{
    internal StellarSearchResult()
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
#endif
