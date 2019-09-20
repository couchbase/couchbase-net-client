using System.Threading;

namespace Couchbase.Management.Query
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
