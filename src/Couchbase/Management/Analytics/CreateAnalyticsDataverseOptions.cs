using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDataverseOptions
    {
        public bool IgnoreIfExists { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public CreateAnalyticsDataverseOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsDataverseOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}