using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDatasetOptions
    {
        public bool IgnoreIfNotExists { get; set; }
        public string DataverseName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public DropAnalyticsDatasetOptions WithIgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExists = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsDatasetOptions WithDataverseName(string dataverseName)
        {
            DataverseName = dataverseName;
            return this;
        }

        public DropAnalyticsDatasetOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}