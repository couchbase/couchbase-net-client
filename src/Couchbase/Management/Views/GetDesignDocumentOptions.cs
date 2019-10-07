using System.Threading;

namespace Couchbase.Management.Views
{
    public class GetDesignDocumentOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetDesignDocumentOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetDesignDocumentOptions Default => new GetDesignDocumentOptions();
    }
}
