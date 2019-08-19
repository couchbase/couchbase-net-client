using System.Threading;

namespace Couchbase.Management
{
    public class CreateCollectionOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public CreateCollectionOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateCollectionOptions Default => new CreateCollectionOptions();
    }
}