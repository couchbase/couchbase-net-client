using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsIndexOptions
    {
        public bool IgnoreIfExists { get; set; }
        public string DataverseName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public CreateAnalyticsIndexOptions WithIgnoreIfExists(bool ignoreIfExists)
        {
            IgnoreIfExists = ignoreIfExists;
            return this;
        }

        public CreateAnalyticsIndexOptions WithDataverseName(string dataverseName)
        {
            DataverseName = dataverseName;
            return this;
        }

        public CreateAnalyticsIndexOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}