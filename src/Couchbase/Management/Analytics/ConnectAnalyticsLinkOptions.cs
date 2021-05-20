using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class ConnectAnalyticsLinkOptions
    {
        internal string LinkNameValue { get; set; } = "Local";

        public ConnectAnalyticsLinkOptions LinkName(string linkName)
        {
            LinkNameValue = linkName;
            return this;
        }

        internal CancellationToken TokenValue { get; set; }

        public ConnectAnalyticsLinkOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public ConnectAnalyticsLinkOptions Timeout(TimeSpan timeout)
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
