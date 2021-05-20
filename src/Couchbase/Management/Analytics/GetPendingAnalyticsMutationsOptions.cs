using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class GetPendingAnalyticsMutationsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetPendingAnalyticsMutationsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public GetPendingAnalyticsMutationsOptions Timeout(TimeSpan timeout)
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
