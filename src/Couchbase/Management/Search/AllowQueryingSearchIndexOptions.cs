using System.Threading;

namespace Couchbase.Management.Search
{
    public class AllowQueryingSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public AllowQueryingSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static AllowQueryingSearchIndexOptions Default => new AllowQueryingSearchIndexOptions();
    }
}
