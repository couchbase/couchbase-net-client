using System.Threading;

using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class BuildDeferredQueryIndexOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public BuildDeferredQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static BuildDeferredQueryIndexOptions Default => new BuildDeferredQueryIndexOptions();
    }
}
