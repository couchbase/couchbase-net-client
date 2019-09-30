using System.Threading;

namespace Couchbase.Management.Views
{
    public class UpsertDesignDocumentOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public UpsertDesignDocumentOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}