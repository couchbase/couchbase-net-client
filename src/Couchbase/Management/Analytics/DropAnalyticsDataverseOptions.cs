using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDataverseOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }

        public DropAnalyticsDataverseOptions IgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExistsValue = ignoreIfNotExists;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public DropAnalyticsDataverseOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public DropAnalyticsDataverseOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        internal AnalyticsOptions CreateAnalyticsOptions()
        {
            return new AnalyticsOptions()
                .CancellationToken(TokenValue)
                .Timeout(TimeoutValue);
        }
    }
}
