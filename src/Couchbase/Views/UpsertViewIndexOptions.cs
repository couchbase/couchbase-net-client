using System;
using System.Threading;

namespace Couchbase.Views
{
    public class UpsertViewIndexOptions
    {
        public bool IsProduction { get; set; }
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public UpsertViewIndexOptions WithIsProduction(bool isProduction)
        {
            IsProduction = isProduction;
            return this;
        }

        public UpsertViewIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public UpsertViewIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static UpsertViewIndexOptions Default => new UpsertViewIndexOptions();
    }
}
