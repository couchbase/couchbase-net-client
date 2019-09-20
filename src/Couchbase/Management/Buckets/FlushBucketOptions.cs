using System.Threading;

namespace Couchbase.Management.Buckets
{
    public class FlushBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public FlushBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static FlushBucketOptions Default => new FlushBucketOptions();
    }
}
