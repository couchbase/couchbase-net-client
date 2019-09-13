using System.Threading;

namespace Couchbase.Management
{
    public class GetSearchIndexDocumentCountOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetSearchIndexDocumentCountOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetSearchIndexDocumentCountOptions Default => new GetSearchIndexDocumentCountOptions();
    }
}