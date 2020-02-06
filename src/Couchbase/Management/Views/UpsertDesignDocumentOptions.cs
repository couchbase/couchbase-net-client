using System.Threading;

#nullable enable

namespace Couchbase.Management.Views
{
    public class UpsertDesignDocumentOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public UpsertDesignDocumentOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static UpsertDesignDocumentOptions Default => new UpsertDesignDocumentOptions();
    }
}
