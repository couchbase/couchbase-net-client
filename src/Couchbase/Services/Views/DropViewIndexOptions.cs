using System;
using System.Threading;

namespace Couchbase.Services.Views
{
    public class DropViewIndexOptions
    {
        public bool IsProduction { get; set; }
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public DropViewIndexOptions WithIsProduction(bool isProduction)
        {
            IsProduction = isProduction;
            return this;
        }

        public DropViewIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public DropViewIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static DropViewIndexOptions Default => new DropViewIndexOptions();
    }
}
