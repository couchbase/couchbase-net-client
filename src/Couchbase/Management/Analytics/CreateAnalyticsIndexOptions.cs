using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsIndexOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }

        public CreateAnalyticsIndexOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsIndexOptions DataverseName(string dataverseName)
        {
            DataverseNameValue = dataverseName;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public CreateAnalyticsIndexOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public CreateAnalyticsIndexOptions Timeout(TimeSpan timeout)
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
