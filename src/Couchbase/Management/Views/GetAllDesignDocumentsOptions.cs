using System.Threading;

namespace Couchbase.Management.Views
{
    public class GetAllDesignDocumentsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllDesignDocumentsOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllDesignDocumentsOptions Default => new GetAllDesignDocumentsOptions();
    }
}
