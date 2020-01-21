using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetAllAnalyticsDatasetsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllAnalyticsDatasetsOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}