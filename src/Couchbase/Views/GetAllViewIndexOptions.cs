using System;
using System.Threading;

namespace Couchbase.Views
{
    public class GetAllViewIndexOptions
    {
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetAllViewIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetAllViewIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetAllViewIndexOptions Default => new GetAllViewIndexOptions();
    }
}
