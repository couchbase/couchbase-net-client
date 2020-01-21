using System.Threading;

namespace Couchbase.Management.Search
{
    public class PauseIngestSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public PauseIngestSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static PauseIngestSearchIndexOptions Default => new PauseIngestSearchIndexOptions();
    }
}
