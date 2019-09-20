using System.Threading;

namespace Couchbase.Management.Collections
{
    public class CollectionExistsOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public CollectionExistsOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CollectionExistsOptions Default => new CollectionExistsOptions();
    }
}
