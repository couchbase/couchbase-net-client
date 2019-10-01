using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetAllAnalyticsIndexesOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetAllAnalyticsIndexesOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}