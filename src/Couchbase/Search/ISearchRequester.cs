#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Core.Compatibility;

namespace Couchbase.Search;

/// <summary>
/// An interface representing the ability to do make a <see cref="SearchRequest" />
/// </summary>
[InterfaceStability(Level.Volatile)]
public interface ISearchRequester
{
    Task<ISearchResult> SearchAsync(string searchIndexName, SearchRequest searchRequest, SearchOptions? options)
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
    {
        throw new NotImplementedException(nameof(SearchAsync));
    }
#else
    // legacy platforms don't support default interface implementations
        ;
#endif
}
