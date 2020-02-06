using System.Threading;

#nullable enable

namespace Couchbase.Management.Search
{
    public class UnfreezePlanSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public UnfreezePlanSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UnfreezePlanSearchIndexOptions Default => new UnfreezePlanSearchIndexOptions();
    }
}
