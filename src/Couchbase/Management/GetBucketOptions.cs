using System.Threading;

namespace Couchbase.Management
{
    public class GetBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetBucketOptions Default => new GetBucketOptions();
    }
}