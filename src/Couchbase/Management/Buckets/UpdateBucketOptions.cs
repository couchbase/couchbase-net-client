using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

namespace Couchbase.Management.Buckets
{
    public class UpdateBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public UpdateBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpdateBucketOptions Default => new UpdateBucketOptions();
    }
}
