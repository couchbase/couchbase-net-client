using System.Threading;

namespace Couchbase.Management
{
    public class GetAllBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllBucketOptions Default => new GetAllBucketOptions();
    }
}