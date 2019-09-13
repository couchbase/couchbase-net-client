using System.Threading;

namespace Couchbase.Management
{
    public class GetAllSearchIndexesOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetAllSearchIndexesOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllSearchIndexesOptions Default => new GetAllSearchIndexesOptions();
    }
}