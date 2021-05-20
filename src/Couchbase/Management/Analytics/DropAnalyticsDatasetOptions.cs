using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDatasetOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }

        public DropAnalyticsDatasetOptions DataverseName(string dataverseName)
        {
            DataverseNameValue = dataverseName;
            return this;
        }

        public DropAnalyticsDatasetOptions IgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExistsValue = ignoreIfNotExists;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public DropAnalyticsDatasetOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public DropAnalyticsDatasetOptions Timeout(TimeSpan timeout)
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
