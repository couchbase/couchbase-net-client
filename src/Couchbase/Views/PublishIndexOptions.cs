using System;
using System.Threading;

namespace Couchbase.Views
{
    public class PublishIndexOptions
    {
        public TimeSpan? Timeout { get; set; }
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        public PublishIndexOptions WithTimeout(TimeSpan timeout)
        {
            Timeout = timeout;
            return this;
        }

        public PublishIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }

        public static PublishIndexOptions Default => new PublishIndexOptions();
    }
}
