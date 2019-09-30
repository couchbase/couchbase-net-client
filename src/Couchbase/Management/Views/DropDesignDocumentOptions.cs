using System.Threading;

namespace Couchbase.Management.Views
{
    public class DropDesignDocumentOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DropDesignDocumentOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}