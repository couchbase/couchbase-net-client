using System.Threading;

#nullable enable

namespace Couchbase.Management.Collections
{
    public class CollectionExistsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public CollectionExistsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        public static CollectionExistsOptions Default => new CollectionExistsOptions();
    }
}
