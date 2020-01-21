using System.Threading;

namespace Couchbase.Management.Search
{
    public class GetAllSearchIndexesOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllSearchIndexesOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllSearchIndexesOptions Default => new GetAllSearchIndexesOptions();
    }
}
