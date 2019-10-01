using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class GetAllAnalyticsDatasetsOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetAllAnalyticsDatasetsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}