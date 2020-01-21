using System.Threading;

namespace Couchbase.Management.Search
{
    public class DisallowQueryingSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public DisallowQueryingSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DisallowQueryingSearchIndexOptions Default => new DisallowQueryingSearchIndexOptions();
    }
}
