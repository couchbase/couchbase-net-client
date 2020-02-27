using System.Threading;

namespace Couchbase.Management.Analytics
{
    public class DisconnectAnalyticsLinkOptions
    {
        internal string LinkNameValue { get; set; } = "Local";
        internal CancellationToken TokenValue { get; set; }

        public DisconnectAnalyticsLinkOptions LinkName(string linkName)
        {
            LinkNameValue = linkName;
            return this;
        }

        public DisconnectAnalyticsLinkOptions CancellationToken(CancellationToken cancellationToken)
        {
            TokenValue = cancellationToken;
            return this;
        }
    }
}
