using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetAllAnalyticsIndexesOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllAnalyticsIndexesOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}