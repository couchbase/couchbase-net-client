using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetPendingAnalyticsMutationsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetPendingAnalyticsMutationsOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}