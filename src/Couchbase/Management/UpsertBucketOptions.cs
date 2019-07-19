using System.Threading;

namespace Couchbase.Management
{
    public class UpsertBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public UpsertBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpsertBucketOptions Default => new UpsertBucketOptions();
    }
}