using System;
using System.Threading;
using Couchbase.Analytics;

namespace Couchbase.Management.Analytics
{
    public class GetAllAnalyticsDatasetsOptions
    {
        internal CancellationToken TokenValue { get; set; }

        public GetAllAnalyticsDatasetsOptions CancellationToken(CancellationToken token)
        {
            TokenValue = token;
            return this;
        }

        internal TimeSpan TimeoutValue { get; set; }

        public GetAllAnalyticsDatasetsOptions Timeout(TimeSpan timeout)
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
