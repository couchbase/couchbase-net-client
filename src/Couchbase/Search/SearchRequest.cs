#nullable enable
using System;
using Couchbase.Core.Compatibility;
using Couchbase.Core.Exceptions;
using Couchbase.KeyValue;
using Couchbase.Search.Queries.Vector;

namespace Couchbase.Search;

/// <summary>
/// A top-level SearchRequest, encompassing all Search sub-request types, including FTS and VectorSearch.
/// </summary>
[InterfaceStability(Level.Committed)]
public sealed record SearchRequest(ISearchQuery? SearchQuery = null, VectorSearch? VectorSearch = null)
{
    public static SearchRequest Create(ISearchQuery searchQuery) => new SearchRequest(SearchQuery: searchQuery);

    public static SearchRequest Create(VectorSearch vectorSearch) => new SearchRequest(VectorSearch: vectorSearch);

    public SearchRequest WithSearchQuery(ISearchQuery searchQuery)
    {
        _ = searchQuery ?? throw new ArgumentNullException(nameof(searchQuery));
        if (this.SearchQuery is not null)
        {
            throw new InvalidArgumentException($"{nameof(SearchQuery)} has already been specified");
        }

        return this with { SearchQuery = searchQuery };
    }

    public SearchRequest WithVectorSearch(VectorSearch vectorSearch)
    {
        _ = vectorSearch ?? throw new ArgumentNullException(nameof(vectorSearch));
        if (this.VectorSearch is not null)
        {
            throw new InvalidArgumentException($"{nameof(VectorSearch)} has already been specified");
        }

        return this with { VectorSearch = vectorSearch };
    }

    public IScope? Scope { get; init; } = null;
}
