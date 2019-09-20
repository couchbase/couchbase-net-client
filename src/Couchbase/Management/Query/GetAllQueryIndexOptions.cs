using System.Threading;

namespace Couchbase.Management.Query
{
    public class GetAllQueryIndexOptions
    {
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllQueryIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllQueryIndexOptions Default => new GetAllQueryIndexOptions();
    }
}
