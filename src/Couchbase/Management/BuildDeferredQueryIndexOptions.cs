using System.Threading;

namespace Couchbase.Management
{
    public class BuildDeferredQueryIndexOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public BuildDeferredQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static BuildDeferredQueryIndexOptions Default => new BuildDeferredQueryIndexOptions();
    }
}