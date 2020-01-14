using System.Threading;

namespace Couchbase.Management.Buckets
{
    public class UpdateBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public UpdateBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpdateBucketOptions Default => new UpdateBucketOptions();
    }
}
