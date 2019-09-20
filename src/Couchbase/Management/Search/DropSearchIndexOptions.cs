using System.Threading;

namespace Couchbase.Management.Search
{
    public class DropSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DropSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropSearchIndexOptions Default => new DropSearchIndexOptions();
    }
}
