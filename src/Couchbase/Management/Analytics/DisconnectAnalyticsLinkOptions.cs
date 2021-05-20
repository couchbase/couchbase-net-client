using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class DisconnectAnalyticsLinkOptions
    {
        internal string LinkNameValue { get; set; } = "Local";

        public DisconnectAnalyticsLinkOptions LinkName(string linkName)
        {
            LinkNameValue = linkName;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public DisconnectAnalyticsLinkOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public DisconnectAnalyticsLinkOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        internal AnalyticsOptions CreateAnalyticsOptions()
        {
            return new AnalyticsOptions()
                .CancellationToken(TokenValue)
                .Timeout(TimeoutValue);
        }
    }
}
