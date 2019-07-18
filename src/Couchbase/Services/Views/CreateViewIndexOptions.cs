using System;
using System.Threading;

namespace Couchbase.Services.Views
{
    public class CreateViewIndexOptions
    {
        public bool IsProduction { get; set; }
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public CreateViewIndexOptions WithIsProduction(bool isProduction)
        {
            IsProduction = isProduction;
            return this;
        }

        public CreateViewIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public CreateViewIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static CreateViewIndexOptions Default => new CreateViewIndexOptions();
    }
}
