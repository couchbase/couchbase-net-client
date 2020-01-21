using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsIndexOptions
    {
        internal bool IgnoreIfNotExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

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

        public DropAnalyticsIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}