using System.Threading;

namespace Couchbase.Management
{
    public class FreezePlanSearchIndexOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public FreezePlanSearchIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static FreezePlanSearchIndexOptions Default => new FreezePlanSearchIndexOptions();
    }
}