using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class ConnectAnalyticsLinkOptions
    {
        internal string LinkNameValue { get; set; } = "Local";
        internal CancellationToken TokenValue { get; set; }

        public ConnectAnalyticsLinkOptions LinkName(string linkName)
        {
            LinkNameValue = linkName;
            return this;
        }

        public ConnectAnalyticsLinkOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}
