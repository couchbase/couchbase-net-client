using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;


namespace Couchbase.Management.Buckets
{
    public class GetAllBucketsOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public GetAllBucketsOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllBucketsOptions Default => new GetAllBucketsOptions();
    }
}
