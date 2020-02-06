using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class CreateCollectionOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public CreateCollectionOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static CreateCollectionOptions Default => new CreateCollectionOptions();
    }
}
