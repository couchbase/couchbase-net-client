using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetPendingAnalyticsMutationsOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetPendingAnalyticsMutationsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}