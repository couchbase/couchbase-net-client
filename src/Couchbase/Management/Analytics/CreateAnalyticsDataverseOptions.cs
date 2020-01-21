using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDataverseOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

        public CreateAnalyticsDataverseOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsDataverseOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}