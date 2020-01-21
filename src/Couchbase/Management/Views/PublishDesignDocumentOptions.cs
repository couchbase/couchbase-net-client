using System.Threading;

namespace Couchbase.Management.Views
{
    public class PublishDesignDocumentOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public PublishDesignDocumentOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static PublishDesignDocumentOptions Default => new PublishDesignDocumentOptions();
    }
}
