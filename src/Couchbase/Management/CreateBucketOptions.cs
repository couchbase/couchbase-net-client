using System.Threading;

namespace Couchbase.Management
{
    public class CreateBucketOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public CreateBucketOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateBucketOptions Default => new CreateBucketOptions();
    }
}