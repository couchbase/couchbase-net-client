using System.Threading;

namespace Couchbase.Management
{
    public class WatchQueryIndexOptions
    {
        public bool WatchPrimary { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public WatchQueryIndexOptions WithWatchPrimary(bool watchPrimary)
        {
            WatchPrimary = watchPrimary;
            return this;
        }

        public WatchQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static WatchQueryIndexOptions Default => new WatchQueryIndexOptions();
    }
}