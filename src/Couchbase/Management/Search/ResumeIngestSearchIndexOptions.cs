using System.Threading;

namespace Couchbase.Management.Search
{
    public class ResumeIngestSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public ResumeIngestSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static ResumeIngestSearchIndexOptions Default => new ResumeIngestSearchIndexOptions();
    }
}
