using System.Threading;

namespace Couchbase.Management.Search
{
    public class GetSearchIndexDocumentCountOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetSearchIndexDocumentCountOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetSearchIndexDocumentCountOptions Default => new GetSearchIndexDocumentCountOptions();
    }
}
