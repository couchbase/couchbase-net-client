using System.Threading;

namespace Couchbase.Management.Collections
{
    public class CreateScopeOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public CreateScopeOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateScopeOptions Default => new CreateScopeOptions();
    }
}
