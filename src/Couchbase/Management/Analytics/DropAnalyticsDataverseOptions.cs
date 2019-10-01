using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDataverseOptions
    {
        public bool IgnoreIfNotExists { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public DropAnalyticsDataverseOptions WithIgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExists = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsDataverseOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}