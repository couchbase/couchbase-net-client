using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class ConnectAnalyticsLinkOptions
    {
        public string LinkName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public ConnectAnalyticsLinkOptions WithLinkName(string linkName)
        {
            LinkName = linkName;
            return this;
        }

        public ConnectAnalyticsLinkOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}