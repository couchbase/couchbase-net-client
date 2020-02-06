using System.Threading;

#nullable enable

namespace Couchbase.Management.Views
{
    public class GetDesignDocumentOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetDesignDocumentOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetDesignDocumentOptions Default => new GetDesignDocumentOptions();
    }
}
