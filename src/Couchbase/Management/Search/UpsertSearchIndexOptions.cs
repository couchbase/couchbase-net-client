using System.Threading;

namespace Couchbase.Management.Search
{
    public class UpsertSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public UpsertSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpsertSearchIndexOptions Default => new UpsertSearchIndexOptions();
    }
}
