using System.Threading;
using System.Threading.Tasks;
using Couchbase.Search;

#nullable enable

namespace Couchbase.Stellar.Search;

/// <summary>
/// This is required to go back in time to the ISearchClient interface before Vector
/// search changes (NCBC-3593) - so that we can properly mock the client for testing.
/// </summary>
internal interface IStellarSearchClient : ISearchClient
{
    public Task<ISearchResult> QueryAsync(string indexName, ISearchQuery query, SearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
