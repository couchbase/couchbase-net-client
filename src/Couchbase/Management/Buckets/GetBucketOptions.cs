using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Buckets
{
    public class GetBucketOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public GetBucketOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetBucketOptions Default => new GetBucketOptions();
    }
}
