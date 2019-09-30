using System;
using System.Threading;

namespace Couchbase.Views
{
    public class GetViewIndexOptions
    {
        public bool IsProduction { get; set; }
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public GetViewIndexOptions WithIsProduction(bool isProduction)
        {
            IsProduction = isProduction;
            return this;
        }

        public GetViewIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public GetViewIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static GetViewIndexOptions Default => new GetViewIndexOptions();
    }
}
