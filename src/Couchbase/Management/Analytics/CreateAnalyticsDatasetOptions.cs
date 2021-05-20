using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDatasetOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal string ConditionValue { get; set; }
        internal string DataverseNameValue { get; set; }

        public CreateAnalyticsDatasetOptions IgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExistsValue = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsDatasetOptions Condition(string condition)
        {
            ConditionValue = condition;
            return this;
        }

        public CreateAnalyticsDatasetOptions DataverseName(string dataverseName)
        {
            DataverseNameValue = dataverseName;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public CreateAnalyticsDatasetOptions  CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public CreateAnalyticsDatasetOptions Timeout(TimeSpan timeout)
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
