using System.Threading;

#nullable enable

namespace Couchbase.Management.Views
{
    public class DropDesignDocumentOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public DropDesignDocumentOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static DropDesignDocumentOptions Default => new DropDesignDocumentOptions();
    }
}
