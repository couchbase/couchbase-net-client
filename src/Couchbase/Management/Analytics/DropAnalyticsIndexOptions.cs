using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DropAnalyticsIndexOptions
    {
        public bool IgnoreIfNotExists { get; set; }
        public string DataverseName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public DropAnalyticsIndexOptions WithIgnoreIfNotExists(bool ignoreIfNotExists)
        {
            IgnoreIfNotExists = ignoreIfNotExists;
            return this;
        }

        public DropAnalyticsIndexOptions WithDataverseName(string dataverseName)
        {
            DataverseName = dataverseName;
            return this;
        }

        public DropAnalyticsIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}