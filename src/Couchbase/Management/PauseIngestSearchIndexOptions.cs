using System.Threading;

namespace Couchbase.Management
{
    public class PauseIngestSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public PauseIngestSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static PauseIngestSearchIndexOptions Default => new PauseIngestSearchIndexOptions();
    }
}