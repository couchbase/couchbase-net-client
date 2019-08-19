using System.Threading;

namespace Couchbase.Management
{
    public class DropCollectionOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DropCollectionOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropCollectionOptions Default => new DropCollectionOptions();
    }
}