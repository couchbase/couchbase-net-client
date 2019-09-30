using System.Threading;

namespace Couchbase.Management.Buckets
{
    public class GetAllBucketsOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllBucketsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllBucketsOptions Default => new GetAllBucketsOptions();
    }
}
