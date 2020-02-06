using System.Threading;
using CancellationTokenCls = System.Threading.CancellationToken;

#nullable enable

namespace Couchbase.Management.Query
{
    public class GetAllQueryIndexOptions
    {
        internal CancellationToken TokenValue { get; set; } = CancellationTokenCls.None;

        public GetAllQueryIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }

        public static GetAllQueryIndexOptions Default => new GetAllQueryIndexOptions();
    }
}
