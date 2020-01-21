using System.Threading;

namespace Couchbase.Management.Search
{
    public class ResumeIngestSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public ResumeIngestSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static ResumeIngestSearchIndexOptions Default => new ResumeIngestSearchIndexOptions();
    }
}
