using System.Threading;

namespace Couchbase.Management.Buckets
{
    public class DropBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropBucketOptions Default => new DropBucketOptions();
    }
}
