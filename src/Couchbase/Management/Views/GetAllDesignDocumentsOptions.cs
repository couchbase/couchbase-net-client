using System.Threading;

namespace Couchbase.Management.Views
{
    public class GetAllDesignDocumentsOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public GetAllDesignDocumentsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllDesignDocumentsOptions Default => new GetAllDesignDocumentsOptions();
    }
}
