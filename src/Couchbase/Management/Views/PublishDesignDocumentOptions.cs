using System.Threading;

namespace Couchbase.Management.Views
{
    public class PublishDesignDocumentOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public PublishDesignDocumentOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static PublishDesignDocumentOptions Default => new PublishDesignDocumentOptions();
    }
}
