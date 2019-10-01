using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DisconnectAnalyticsLinkOptions
    {
        public string LinkName { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public DisconnectAnalyticsLinkOptions WithLinkName(string linkName)
        {
            LinkName = linkName;
            return this;
        }

        public DisconnectAnalyticsLinkOptions WithCancellationToken(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
            return this;
        }
    }
}