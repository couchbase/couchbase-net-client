using System.Threading;

namespace Couchbase.Management.Collections
{
    public class DropScopeOptions
    {
        public CancellationToken CancellationToken { get; set; }

        public DropScopeOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropScopeOptions Default => new DropScopeOptions();
    }
}
