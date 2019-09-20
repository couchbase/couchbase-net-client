using System.Threading;

namespace Couchbase.Management.Search
{
    public class AllowQueryingSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public AllowQueryingSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static AllowQueryingSearchIndexOptions Default => new AllowQueryingSearchIndexOptions();
    }
}
