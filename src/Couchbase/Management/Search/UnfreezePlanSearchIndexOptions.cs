using System.Threading;

namespace Couchbase.Management.Search
{
    public class UnfreezePlanSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public UnfreezePlanSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UnfreezePlanSearchIndexOptions Default => new UnfreezePlanSearchIndexOptions();
    }
}
