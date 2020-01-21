using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Buckets
{
    public class DropCollectionOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public DropCollectionOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropCollectionOptions Default => new DropCollectionOptions();
    }
}
