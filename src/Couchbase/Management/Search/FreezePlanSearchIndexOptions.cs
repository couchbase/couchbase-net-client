using System.Threading;

namespace Couchbase.Management.Search
{
    public class FreezePlanSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public FreezePlanSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static FreezePlanSearchIndexOptions Default => new FreezePlanSearchIndexOptions();
    }
}
