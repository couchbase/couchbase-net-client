using System.Threading;

namespace Couchbase.Management
{
    public class UpsertSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public UpsertSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpsertSearchIndexOptions Default => new UpsertSearchIndexOptions();
    }
}