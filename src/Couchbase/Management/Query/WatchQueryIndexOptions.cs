using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Query
{
    public class WatchQueryIndexOptions
    {
        internal bool WatchPrimaryValue { get; set; }
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public WatchQueryIndexOptions WatchPrimary(bool watchPrimary)
        {
            WatchPrimaryValue = watchPrimary;
            return this;
        }

        public WatchQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static WatchQueryIndexOptions Default => new WatchQueryIndexOptions();
    }
}
