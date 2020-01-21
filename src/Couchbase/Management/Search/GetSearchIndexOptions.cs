using System.Threading;

namespace Couchbase.Management.Search
{
    public class GetSearchIndexOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetSearchIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetSearchIndexOptions Default => new GetSearchIndexOptions();
    }
}
