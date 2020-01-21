using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsDatasetOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

        public DropAnalyticsDatasetOptions IgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExistsValue = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsDatasetOptions DataverseName(string dataverseName)
        {
            DataverseNameValue = dataverseName;
            return this;
        }

        public DropAnalyticsDatasetOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}