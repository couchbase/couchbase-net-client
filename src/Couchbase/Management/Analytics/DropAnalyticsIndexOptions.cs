using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsIndexOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }

        public DropAnalyticsIndexOptions IgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExistsValue = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsIndexOptions DataverseName(string dataverseName)
        {
            DataverseNameValue = dataverseName;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public  DropAnalyticsIndexOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public  DropAnalyticsIndexOptions Timeout(TimeSpan timeout)
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
