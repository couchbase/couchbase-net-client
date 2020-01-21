using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDataverseOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

        public DropAnalyticsDataverseOptions IgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExistsValue = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsDataverseOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}