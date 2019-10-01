using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsDatasetOptions
    {
        public bool IgnoreIfExists { get; set; }
        public string Condition { get; set; }
        public string DataverseName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public CreateAnalyticsDatasetOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsDatasetOptions WithCondition(string condition)
        {
            Condition = condition;
            return this;
        }

        public CreateAnalyticsDatasetOptions WithDataverseName(string dataverseName)
        {
            DataverseName = dataverseName;
            return this;
        }

        public CreateAnalyticsDatasetOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}