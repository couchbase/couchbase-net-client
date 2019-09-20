using System.Threading;

namespace Couchbase.Management.Search
{
    public class GetSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetSearchIndexOptions Default => new GetSearchIndexOptions();
    }
}
