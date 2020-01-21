using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class CreateAnalyticsIndexOptions
    {
        internal bool IgnoreIfExistsValue { get; set; }
        internal string DataverseNameValue { get; set; }
        internal CancellationToken TokenValue { get; set; }

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

        public CreateAnalyticsIndexOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}