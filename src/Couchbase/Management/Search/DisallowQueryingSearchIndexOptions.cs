using System.Threading;

namespace Couchbase.Management.Search
{
    public class DisallowQueryingSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DisallowQueryingSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DisallowQueryingSearchIndexOptions Default => new DisallowQueryingSearchIndexOptions();
    }
}
