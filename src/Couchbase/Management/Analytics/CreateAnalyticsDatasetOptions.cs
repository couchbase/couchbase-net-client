using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDatasetOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal string ConditionValue { get; set; }
        internal string DataverseNameValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

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

        public CreateAnalyticsDatasetOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}